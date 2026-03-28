using System;
using System.Windows.Threading;

namespace LibVideo.Helpers
{
    public class Debouncer
    {
        private readonly DispatcherTimer _timer;

        public Debouncer(TimeSpan delay)
        {
            _timer = new DispatcherTimer { Interval = delay };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                Action?.Invoke();
            };
        }

        private Action Action { get; set; }

        public void Debounce(Action action)
        {
            Action = action;
            _timer.Stop();
            _timer.Start();
        }

        public void Cancel()
        {
            _timer.Stop();
        }
    }
}
