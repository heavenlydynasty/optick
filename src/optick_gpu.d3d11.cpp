#include "optick.config.h"
#if USE_OPTICK
#if OPTICK_ENABLE_GPU_D3D11

#include "optick_common.h"
#include "optick_memory.h"
#include "optick_core.h"
#include "optick_gpu.h"
#include "optick_core.platform.h"

#include <atomic>
#include <thread>

#include <d3d11.h>
#include <d3d11_1.h>
#include <dxgi.h>
#include <dxgi1_2.h>

#define OPTICK_CHECK(args) do { HRESULT __hr = args; (void)__hr; OPTICK_ASSERT(__hr == S_OK, "Failed check"); } while(false);

namespace Optick
{
    class GPUProfilerD3D11 : public GPUProfiler
    {
        struct Frame
        {
            Frame();
            ~Frame();

            void Reset();
            void Shutdown();
        };

        struct NodePayload
        {
            ID3D11DeviceContext* context;
            ID3DUserDefinedAnnotation* annotation;
            array<ID3D11Query*, MAX_QUERIES_COUNT> stamps;
            array<Frame, NUM_FRAMES_DELAY> frames;

            NodePayload();
            ~NodePayload();
        };

        vector<NodePayload*> nodePayloads;

        ID3D11Device* device;
        ID3D11Query* disjoint;
        bool measurable;

        // VSync Stats
        DXGI_FRAME_STATISTICS prevFrameStatistics;

        //void UpdateRange(uint32_t start, uint32_t finish)
        void InitNodeInternal(const char* nodeName, uint32_t nodeIndex, ID3D11DeviceContext* context);

        void ResolveTimestamps(uint32_t startIndex, uint32_t count);

        void WaitForFrame(uint64_t frameNumber);

    public:
        GPUProfilerD3D11();
        ~GPUProfilerD3D11();

        void InitDevice(ID3D11Device* pDevice, ID3D11DeviceContext** pDeviceContext, uint32_t numCommandQueues);

        void QueryTimestamp(ID3D11DeviceContext* context, int64_t* outCpuTimestamp);

        void Flip(IDXGISwapChain* swapChain);

        void BeginFrame() override;
        void EndFrame() override;

        // Interface implementation
        ClockSynchronization GetClockSynchronization(uint32_t nodeIndex) override;

        void QueryTimestamp(void* context, int64_t* outCpuTimestamp) override
        {
            QueryTimestamp((ID3D11DeviceContext*)context, outCpuTimestamp);
        }

        void Flip(void* swapChain) override
        {
            Flip(static_cast<IDXGISwapChain*>(swapChain));
        }

        void BeginDrawEvent(uint64_t color, char const* formatString, ...) override;
        void EndDrawEvent() override;
    };

    template <class T>
    void SafeRelease(T** ppT)
    {
        if (*ppT)
        {
            (*ppT)->Release();
            *ppT = NULL;
        }
    }

    void InitGpuD3D11(ID3D11Device* device, ID3D11DeviceContext** cmdQueues, uint32_t numQueues)
    {
        GPUProfilerD3D11* gpuProfiler = Memory::New<GPUProfilerD3D11>();
        gpuProfiler->InitDevice(device, cmdQueues, numQueues);
        Core::Get().InitGPUProfiler(gpuProfiler);
    }

    GPUProfilerD3D11::GPUProfilerD3D11()
        : device(nullptr)
        , disjoint(nullptr)
        , measurable(false)
    {
        //prevFrameStatistics = { 0 };
        memset(&prevFrameStatistics, 0, sizeof(prevFrameStatistics));
    }

    GPUProfilerD3D11::~GPUProfilerD3D11()
    {
        //WaitForFrame(frameNumber - 1);

        for (NodePayload* payload : nodePayloads)
            Memory::Delete(payload);
        nodePayloads.clear();

        for (Node* node : nodes)
            Memory::Delete(node);
        nodes.clear();

        if (disjoint)
            disjoint->Release();
    }

    void GPUProfilerD3D11::InitDevice(ID3D11Device* pDevice, ID3D11DeviceContext** pDeviceContext, uint32_t numCommandQueues)
    {
        device = pDevice;

        uint32_t nodeCount = numCommandQueues; // device->GetNodeCount();

        nodes.resize(nodeCount);
        nodePayloads.resize(nodeCount);

        // Get Device Name
        IDXGIDevice2* device2 = nullptr;
        IDXGIAdapter1* adapter = nullptr;

        pDevice->QueryInterface(__uuidof(IDXGIDevice2), (void**)& device2);

        if (device2)
        {
            device2->GetParent(__uuidof(IDXGIAdapter1), (void**)& adapter);
        }

        DXGI_ADAPTER_DESC1 desc;
        memset(&desc, 0, sizeof(desc));
        if (adapter)
        {
            adapter->GetDesc1(&desc);
        }

        if (adapter)
        {
            adapter->Release();
        }

        if (device2)
        {
            device2->Release();
        }

        char deviceName[128] = {0};
        wcstombs_s(deviceName, desc.Description, OPTICK_ARRAY_SIZE(deviceName) - 1);

        for (uint32_t nodeIndex = 0; nodeIndex < nodeCount; ++nodeIndex)
            InitNodeInternal(deviceName, nodeIndex, pDeviceContext[nodeIndex]);

        D3D11_QUERY_DESC queryDisjointDesc = {D3D11_QUERY_TIMESTAMP_DISJOINT, 0};
        device->CreateQuery(&queryDisjointDesc, &disjoint);
    }

    void GPUProfilerD3D11::InitNodeInternal(const char* nodeName, uint32_t nodeIndex, ID3D11DeviceContext* context)
    {
        GPUProfiler::InitNode(nodeName, nodeIndex);

        NodePayload* node = Memory::New<NodePayload>();
        node->context = context;
        context->QueryInterface(__uuidof(ID3DUserDefinedAnnotation), reinterpret_cast<void**> (&node->annotation));
        nodePayloads[nodeIndex] = node;
        for (uint32_t i = 0; i < MAX_QUERIES_COUNT; ++i)
        {
            D3D11_QUERY_DESC queryTimestampDesc = {D3D11_QUERY_TIMESTAMP, 0};
            device->CreateQuery(&queryTimestampDesc, &node->stamps[i]);
        }
    }

    void GPUProfilerD3D11::QueryTimestamp(ID3D11DeviceContext* context, int64_t* outCpuTimestamp)
    {
        OPTICK_UNUSED(context);
        if (currentState == STATE_RUNNING && measurable)
        {
            const uint32_t index = nodes[currentNode]->QueryTimestamp(outCpuTimestamp);
            NodePayload* payload = nodePayloads[currentNode];
            payload->context->End(payload->stamps[index]);
        }
    }

    void GPUProfilerD3D11::ResolveTimestamps(uint32_t startIndex, uint32_t count)
    {
        OPTICK_DETAIL_EVENT();
        OPTICK_TAG("start", startIndex);
        OPTICK_TAG("count", count);
        // Don't ask twice (API violation)
        if (count)
        {
            Node* node = nodes[currentNode];
            NodePayload* payload = nodePayloads[currentNode];

            // Convert GPU timestamps => CPU Timestamps
            for (uint32_t index = startIndex; index < startIndex + count; ++index)
            {
                OPTICK_EXTEND_EVENT("GPUProfilerD3D11::ResolveTimestamps::GetData");
                if (S_OK != payload->context->GetData(
                    payload->stamps[index],
                    &node->queryGpuTimestamps[index],
                    payload->stamps[index]->GetDataSize(),
                    0))
                {
                    node->queryGpuTimestamps[index] = 0;
                }
                *node->queryCpuTimestamps[index] = node->clock.GetCPUTimestamp(node->queryGpuTimestamps[index]);
            }
        }
    }

    void GPUProfilerD3D11::WaitForFrame(uint64_t frameNumberToWait)
    {
        OPTICK_EVENT();
        OPTICK_UNUSED(frameNumberToWait);
    }

    void GPUProfilerD3D11::BeginFrame()
    {
        if (currentState == STATE_RUNNING)
        {
            measurable = true;
            NodePayload* payload = nodePayloads[currentNode];
            payload->context->Begin(disjoint);
        }
    }

    void GPUProfilerD3D11::EndFrame()
    {
        if (currentState == STATE_RUNNING && measurable)
        {
            NodePayload* payload = nodePayloads[currentNode];
            ID3D11DeviceContext* context = payload->context;
            context->End(disjoint);
            D3D11_QUERY_DATA_TIMESTAMP_DISJOINT disjointData = { 0 };
            while (S_OK != context->GetData(disjoint, &disjointData, disjoint->GetDataSize(), 0));
            measurable = false;
        }
    }

    void GPUProfilerD3D11::BeginDrawEvent(uint64_t color, char const* formatString, ...)
    {
        OPTICK_UNUSED(color);
        NodePayload* payload = nodePayloads[currentNode];
        payload->annotation->BeginEvent(Platform::StringtoWstring(formatString).c_str());
    }

    void GPUProfilerD3D11::EndDrawEvent()
    {
        NodePayload* payload = nodePayloads[currentNode];
        payload->annotation->EndEvent();
    }

    void GPUProfilerD3D11::Flip(IDXGISwapChain* swapChain)
    {
        OPTICK_CATEGORY("GPUProfilerD3D11::Flip", Category::Rendering);

        std::lock_guard<std::recursive_mutex> lock(updateLock);

        if (currentState == STATE_STARTING)
            currentState = STATE_RUNNING;

        if (currentState == STATE_RUNNING && !Core::Get().IsProfilerDrawEvent())
        {
            Node& node = *nodes[currentNode];
            NodePayload* payload = nodePayloads[currentNode];
            ID3D11DeviceContext* context = payload->context;

            uint32_t currentFrameIndex = frameNumber % NUM_FRAMES_DELAY;
            uint32_t nextFrameIndex = (frameNumber + 1) % NUM_FRAMES_DELAY;

            //Frame& currentFrame = frames[frameNumber % NUM_FRAMES_DELAY];
            //Frame& nextFrame = frames[(frameNumber + 1) % NUM_FRAMES_DELAY];

            QueryFrame& currentFrame = node.queryGpuframes[currentFrameIndex];
            QueryFrame& nextFrame = node.queryGpuframes[nextFrameIndex];

            if (EventData* frameEvent = currentFrame.frameEvent)
                QueryTimestamp(context, &frameEvent->finish);

            // Generate GPU Frame event for the next frame
            EventData& event = AddFrameEvent();
            QueryTimestamp(context, &event.start);
            QueryTimestamp(context, &AddFrameTag().timestamp);
            nextFrame.frameEvent = &event;

            uint32_t queryBegin = currentFrame.queryIndexStart;
            uint32_t queryEnd = node.queryIndex;

            EndFrame();

            if (queryBegin != (uint32_t)-1)
            {
                OPTICK_ASSERT(queryEnd - queryBegin <= MAX_QUERIES_COUNT, "Too many queries in one frame? Increase GPUProfiler::MAX_QUERIES_COUNT to fix the problem!");
                currentFrame.queryIndexCount = queryEnd - queryBegin;

                uint32_t startIndex = queryBegin % MAX_QUERIES_COUNT;
                uint32_t finishIndex = queryEnd % MAX_QUERIES_COUNT;

                if (startIndex < finishIndex)
                {
                    ResolveTimestamps(startIndex, queryEnd - queryBegin);
                }
                else
                {
                    ResolveTimestamps(startIndex, MAX_QUERIES_COUNT - startIndex);
                    ResolveTimestamps(0, finishIndex);
                }
            }

            // Preparing Next Frame
            // Try resolve timestamps for the current frame
            if (frameNumber >= NUM_FRAMES_DELAY && nextFrame.queryIndexCount)
            {
                WaitForFrame(frameNumber + 1 - NUM_FRAMES_DELAY);

                uint32_t resolveStart = nextFrame.queryIndexStart % MAX_QUERIES_COUNT;
                uint32_t resolveFinish = resolveStart + nextFrame.queryIndexCount;
                ResolveTimestamps(resolveStart, std::min<uint32_t>(resolveFinish, MAX_QUERIES_COUNT) - resolveStart);
                if (resolveFinish > MAX_QUERIES_COUNT)
                    ResolveTimestamps(0, resolveFinish - MAX_QUERIES_COUNT);
            }

            nextFrame.queryIndexStart = queryEnd;
            nextFrame.queryIndexCount = 0;

            // Process VSync
            DXGI_FRAME_STATISTICS currentFrameStatistics = {0};
            HRESULT result = swapChain->GetFrameStatistics(&currentFrameStatistics);
            if ((result == S_OK) && (prevFrameStatistics.PresentCount + 1 == currentFrameStatistics.PresentCount))
            {
                EventData& data = AddVSyncEvent();
                data.start = prevFrameStatistics.SyncQPCTime.QuadPart;
                data.finish = currentFrameStatistics.SyncQPCTime.QuadPart;
            }
            prevFrameStatistics = currentFrameStatistics;
        }

        ++frameNumber;
    }

    GPUProfiler::ClockSynchronization GPUProfilerD3D11::GetClockSynchronization(uint32_t nodeIndex)
    {
        OPTICK_UNUSED(nodeIndex);

        NodePayload* payload = nodePayloads[currentNode];
        ID3D11DeviceContext* context = payload->context;

        ID3D11Query* queryDisjoint = nullptr;
        ID3D11Query* queryTimestamp = nullptr;

        D3D11_QUERY_DESC queryTimestampDesc = {D3D11_QUERY_TIMESTAMP, 0};
        device->CreateQuery(&queryTimestampDesc, &queryTimestamp);
        D3D11_QUERY_DESC descDisjoint = {D3D11_QUERY_TIMESTAMP_DISJOINT, 0};
        device->CreateQuery(&descDisjoint, &queryDisjoint);

        context->Begin(queryDisjoint);
        context->End(queryTimestamp);
        context->End(queryDisjoint);

        D3D11_QUERY_DATA_TIMESTAMP_DISJOINT disjointData = {0};
        while (S_OK != context->GetData(queryDisjoint, &disjointData, disjoint->GetDataSize(), 0));
        uint64_t timestamp = 0;
        while (S_OK != context->GetData(queryTimestamp, &timestamp, queryTimestamp->GetDataSize(), 0));

        queryTimestamp->Release();
        queryDisjoint->Release();

        ClockSynchronization clock;
        clock.frequencyCPU = GetHighPrecisionFrequency();
        clock.frequencyGPU = disjointData.Frequency;
        clock.timestampCPU = GetHighPrecisionTime();
        clock.timestampGPU = timestamp;
        return clock;
    }

    GPUProfilerD3D11::Frame::Frame()
    {
    }

    GPUProfilerD3D11::Frame::~Frame()
    {
    }

    void GPUProfilerD3D11::Frame::Reset()
    {
    }

    void GPUProfilerD3D11::Frame::Shutdown()
    {
    }

    GPUProfilerD3D11::NodePayload::NodePayload(): context(nullptr), annotation(nullptr)
    {
        stamps.fill(nullptr);
    }

    GPUProfilerD3D11::NodePayload::~NodePayload()
    {
        context = nullptr;
        for (uint32_t i = 0; i < MAX_QUERIES_COUNT; ++i)
        {
            stamps[i]->Release();
        }

        if (annotation)
            annotation->Release();
    }
}

#else
#include "optick_common.h"

namespace Optick
{
	void InitGpuD3D11(ID3D11Device* /*device*/, ID3D11DeviceContext** /*cmdQueues*/, uint32_t /*numQueues*/)
	{
		OPTICK_FAILED("OPTICK_ENABLE_GPU_D3D11 is disabled! Can't initialize GPU Profiler!");
	}
}

#endif //OPTICK_ENABLE_GPU_D3D11
#endif //USE_OPTICK
