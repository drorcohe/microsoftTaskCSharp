using System.Collections.Generic;

namespace AoratoExercise
{
    internal abstract class RequestHandler
    {
        public enum HandlerType { Static, Dynamic }

        protected const int MaxRequestsInFrame = 5;
        protected const int TimeFrameMs = 5000;
        public abstract bool ProcessRequest(long timestampMs);

        public static RequestHandler GetHandler(HandlerType type)
        {
            if (type == HandlerType.Static)
            {
                return new StaticRequestHandler();
            }
            return new DynamicRequestHandler();
        }
    }

    internal class StaticRequestHandler : RequestHandler
    {
        private long _startTimestampMs;
        private int _requestCount;

        public override bool ProcessRequest(long timestampMs)
        {
            if (_requestCount >= MaxRequestsInFrame && _startTimestampMs + TimeFrameMs > timestampMs)
            {
                return false;
            }

            if (_startTimestampMs + TimeFrameMs < timestampMs)
            {
                _startTimestampMs = timestampMs;
                _requestCount = 0;
            }

            _requestCount++;
            return true;
        }
    }

    internal class DynamicRequestHandler : RequestHandler
    {
        private readonly LinkedList<long> _timestamps = new LinkedList<long>();
        public override bool ProcessRequest(long timestampMs)
        {

            if (_timestamps.Count == MaxRequestsInFrame && _timestamps.Last.Value + TimeFrameMs < timestampMs)
            {
                _timestamps.RemoveLast();
            }

            if (_timestamps.Count == MaxRequestsInFrame)
            {
                return false;
            }

            _timestamps.AddFirst(timestampMs);
            return true;

        }
    }

}
