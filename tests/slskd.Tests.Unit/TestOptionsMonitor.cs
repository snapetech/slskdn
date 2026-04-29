// <copyright file="TestOptionsMonitor.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

// <copyright file="TestOptionsMonitor.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Moq;

namespace slskd.Tests.Unit
{
    public class TestOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
    {
        private readonly List<Action<TOptions, string>> _listeners = new();

        public TestOptionsMonitor(TOptions currentValue)
        {
            CurrentValue = currentValue;
        }

        public TOptions CurrentValue { get; private set; }
        public int ListenerCount => _listeners.Count;

        public TOptions Get(string name)
        {
            return CurrentValue;
        }

        public void Set(TOptions value)
        {
            CurrentValue = value;
            foreach (var listener in _listeners.ToArray())
            {
                listener.Invoke(value, null);
            }
        }

        public IDisposable OnChange(Action<TOptions, string> listener)
        {
            _listeners.Add(listener);
            var registration = new Mock<IDisposable>();
            registration.Setup(x => x.Dispose()).Callback(() => _listeners.Remove(listener));
            return registration.Object;
        }

        public void RaiseOnChange(TOptions options)
        {
            foreach (var listener in _listeners.ToArray())
            {
                listener(options, null);
            }
        }
    }
}
