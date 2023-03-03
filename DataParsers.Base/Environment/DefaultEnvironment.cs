using System;
using DataParsers.Base.Providers;

namespace DataParsers.Base.Environment;

public class DefaultEnvironment
{

    public DefaultEnvironment(ILogProvider log, IMetricProvider metricProvider)
    {
        Log = log ?? throw new Exception("Empty ILog in DefaultEnvironment");
        MetricProvider = metricProvider ?? throw new Exception("Empty IChickenMetrics in DefaultEnvironment");
    }

    public ILogProvider Log { get; }
    public IMetricProvider MetricProvider { get; }
}
