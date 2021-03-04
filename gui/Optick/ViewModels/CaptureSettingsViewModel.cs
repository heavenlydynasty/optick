using Profiler.Controls;
using Profiler.Data;
using Profiler.InfrastructureMvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Profiler.ViewModels
{
	class CaptureSettingsViewModel : BaseViewModel
	{
		public class Setting : BaseViewModel
		{
			public String Name { get; set; }
			public String Description { get; set; }

			public Setting(String name, String description)
			{
				Name = name;
				Description = description;
			}
		}

		public class Flag : Setting
		{
			public bool IsEnabled { get; set; }

			public Mode Mask { get; set; }
			public Flag(String name, String description, Mode mask, bool isEnabled) : base(name, description)
			{

				Mask = mask;
				IsEnabled = isEnabled;
			}
		}


		public class Numeric : Setting
		{
			public Numeric(String name, String description) : base(name, description) { }
			public virtual double Value { get; set; }
		}

		public class NumericDelegate : Numeric
		{
			public NumericDelegate(string name, string description) : base(name, description) { }
			public Func<double> Getter { get; set; }
			public Action<double> Setter { get; set; }
			public override double Value { get { return Getter(); } set { Setter(value); } }

		}

		public enum SamplingFrequency
		{
			None = 0,
			Low = 1000,
			Medium = 10000,
			High = 20000,
			Max = 40000,
		}

        public enum Granularity
        {
            None,
            Simple,
            Summary,
            Detail,
            Extend,
            Max,
        }

		public ObservableCollection<Flag> FlagSettings { get; set; } = new ObservableCollection<Flag>(new Flag[]
		{
			//new Flag("Categories", "Collect OPTICK_CATEGORY events", Mode.INSTRUMENTATION_CATEGORIES, true),
			//new Flag("Events", "Collect OPTICK_EVENT events", Mode.INSTRUMENTATION_EVENTS, true),
            //new Flag("Live", "Collect events with live mode", Mode.LIVE, false),
			new Flag("Tags", "Collect OPTICK_TAG events", Mode.TAGS, false),
			new Flag("Switch Contexts", "Collect Switch Context events (kernel)", Mode.SWITCH_CONTEXT, false),
			new Flag("Autosampling", "Sample all threads (kernel)", Mode.AUTOSAMPLING, false),
			new Flag("SysCalls", "Collect system calls ", Mode.SYS_CALLS, false),
			new Flag("GPU", "Collect GPU events", Mode.GPU, false),
            new Flag("GPU Debug", "Collect GPU draw events", Mode.GPUDEBUG, false),
			new Flag("Offline", "Collect events with a opt file at client work directory", Mode.OFFLINE, false),
			new Flag("All Processes", "Collects information about other processes (thread pre-emption)", Mode.OTHER_PROCESSES, false),
		});

		public Array SamplingFrequencyList
		{
			get { return Enum.GetValues(typeof(SamplingFrequency)); }
		}


		private SamplingFrequency _samplingFrequency = SamplingFrequency.Low;
		public SamplingFrequency SamplingFrequencyHz
		{
			get { return _samplingFrequency; }
			set { SetProperty(ref _samplingFrequency, value); }
		}

        public Array CpuGranularityList
        {
            get { return Enum.GetValues(typeof(Granularity)); }
        }

        private Granularity cpuGranularity_ = Granularity.Summary;
        public Granularity CpuGranularityLv
        {
            get { return cpuGranularity_; }
            set { SetProperty(ref cpuGranularity_, value); }
        }

        public Array GpuGranularityList
        {
            get { return Enum.GetValues(typeof(Granularity)); }
        }

        private Granularity gpuGranularity_ = Granularity.Summary;
        public Granularity GpuGranularityLv
        {
            get { return gpuGranularity_; }
            set { SetProperty(ref gpuGranularity_, value); }
        }

		// Frame Limits
		Numeric FrameCountLimit = new Numeric("Frame Count Limit", "Automatically stops capture after selected number of frames") { Value = 0 };
		Numeric TimeLimitSec = new Numeric("Time Limit (sec)", "Automatically stops capture after selected number of seconds") { Value = 0 };
		Numeric MaxSpikeLimitMs = new Numeric("Max Spike (ms)", "Automatically stops capture after selected spike") { Value = 0 };
		Numeric MinFilterLimitMs = new Numeric("Min Filter (ms)", "Filter frame with min value ") { Value = 0 };
		Numeric MaxFilterLimitMs = new Numeric("Max Filter (ms)", "Filter Frame with max value") { Value = 0 };
		public ObservableCollection<Numeric> CaptureLimits { get; set; } = new ObservableCollection<Numeric>();

		// Timeline Settings
		public NumericDelegate TimelineMinThreadDepth { get; private set; } = new NumericDelegate("Collapsed Thread Depth", "Limits the maximum visualization depth for each thread in collapsed mode")
		{
			Getter = () => Settings.LocalSettings.Data.ThreadSettings.CollapsedMaxThreadDepth,
			Setter = (val) => { Settings.LocalSettings.Data.ThreadSettings.CollapsedMaxThreadDepth = (int)val; Settings.LocalSettings.Save(); }
		};

		public NumericDelegate TimelineMaxThreadDepth { get; private set; } = new NumericDelegate("Expanded Thread Depth", "Limits the maximum visualization depth for each thread in expanded modes")
		{
			Getter = ()=> Controls.Settings.LocalSettings.Data.ThreadSettings.ExpandedMaxThreadDepth,
			Setter = (val) => { Controls.Settings.LocalSettings.Data.ThreadSettings.ExpandedMaxThreadDepth = (int)val; Settings.LocalSettings.Save(); }
		};

		public Array ExpandModeList
		{
			get { return Enum.GetValues(typeof(ExpandMode)); }
		}

		public ExpandMode ExpandMode 
		{
			get
			{
				return Controls.Settings.LocalSettings.Data.ThreadSettings.ThreadExpandMode;
			}
			set
			{
				Controls.Settings.LocalSettings.Data.ThreadSettings.ThreadExpandMode = value;
				Controls.Settings.LocalSettings.Save();
			}
		}

		public ObservableCollection<Numeric> TimelineSettings { get; set; } = new ObservableCollection<Numeric>();

		public CaptureSettingsViewModel()
		{
			CaptureLimits.Add(FrameCountLimit);
			CaptureLimits.Add(TimeLimitSec);
			CaptureLimits.Add(MaxSpikeLimitMs);
			CaptureLimits.Add(MinFilterLimitMs);
			CaptureLimits.Add(MaxFilterLimitMs);

			TimelineSettings.Add(TimelineMinThreadDepth);
			TimelineSettings.Add(TimelineMaxThreadDepth);
		}

		public CaptureSettings GetSettings()
		{
			CaptureSettings settings = CaptureSettings.Instance();
			settings.Mode = (Mode.INSTRUMENTATION_CATEGORIES | Mode.INSTRUMENTATION_EVENTS);

			foreach (Flag flag in FlagSettings)
				if (flag.IsEnabled)
					settings.Mode = settings.Mode | flag.Mask;

			settings.SamplingFrequencyHz = (uint)SamplingFrequencyHz;

            settings.CpuGranularityLv = (uint) CpuGranularityLv;
            settings.GpuGranularityLv = (uint)GpuGranularityLv;

			settings.FrameLimit = (uint)FrameCountLimit.Value;
			settings.TimeLimitUs = (uint)(TimeLimitSec.Value * 1000000);
			settings.MaxSpikeLimitUs = (uint)(MaxSpikeLimitMs.Value * 1000);
			settings.MinFilterLimitUs = (uint)(MinFilterLimitMs.Value);
			settings.MaxFilterLimitUs = (uint)(MaxFilterLimitMs.Value);

			settings.MemoryLimitMb = 0;

			return settings;
		}
	}

	public class SamplingFrequencyConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is CaptureSettingsViewModel.SamplingFrequency)
			{
				return String.Format("{0}/sec", (int)(value));
			}

			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
    public class GranularityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is CaptureSettingsViewModel.Granularity)
            {
                return String.Format("{0}", (int)(value));
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
