namespace Improbable.OnlineServices.Common.Analytics
{
    public static class AnalyticsSenderExtensions
    {
        /// <summary>
        /// A utility method for wrapping an analytics sender to provide a default event class
        /// </summary>
        public static AnalyticsSenderClassWrapper WithEventClass(this IAnalyticsSender sender, string eventClass)
        {
            return new AnalyticsSenderClassWrapper(sender, eventClass);
        }
    }
}
