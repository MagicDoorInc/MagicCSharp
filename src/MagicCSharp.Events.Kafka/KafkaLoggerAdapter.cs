using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.Kafka;

/// <summary>
///     Adapter for bridging Kafka logging to Microsoft.Extensions.Logging.
/// </summary>
public static class KafkaLoggerAdapter
{
    /// <summary>
    ///     Create a log handler for Kafka producer.
    /// </summary>
    public static Action<IProducer<TKey, TValue>, LogMessage> GetProducerLogHandler<TKey, TValue>(ILogger logger)
    {
        return (producer, logMessage) =>
        {
            var logLevel = ConvertLogLevel(logMessage.Level);
            logger.Log(logLevel, "{KafkaMessage}", logMessage.Message);
        };
    }

    /// <summary>
    ///     Create a log handler for Kafka consumer.
    /// </summary>
    public static Action<IConsumer<TKey, TValue>, LogMessage> GetConsumerLogHandler<TKey, TValue>(ILogger logger)
    {
        return (consumer, logMessage) =>
        {
            var logLevel = ConvertLogLevel(logMessage.Level);
            logger.Log(logLevel, "{KafkaMessage}", logMessage.Message);
        };
    }

    private static LogLevel ConvertLogLevel(SyslogLevel syslogLevel)
    {
        return syslogLevel switch
        {
            SyslogLevel.Emergency => LogLevel.Critical,
            SyslogLevel.Alert => LogLevel.Critical,
            SyslogLevel.Critical => LogLevel.Critical,
            SyslogLevel.Error => LogLevel.Error,
            SyslogLevel.Warning => LogLevel.Warning,
            SyslogLevel.Notice => LogLevel.Information,
            SyslogLevel.Info => LogLevel.Information,
            SyslogLevel.Debug => LogLevel.Debug,
            _ => LogLevel.Trace,
        };
    }
}