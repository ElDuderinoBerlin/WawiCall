namespace DtmfDetection
{
    using System;

    public class DtmfChangeHandler
    {
        private enum State
        {
            NoDtmf,
            Dtmf
        }

        private State _currentState = State.NoDtmf;

        private DtmfTone _lastTone = DtmfTone.None;

        private object? _clientData;

        public void Handle<T>(DtmfTone tone, Func<DtmfTone, T> handleStart, Action<T, DtmfTone> handleEnd)
        {
            switch (_currentState)
            {
                case State.NoDtmf:
                    if (tone != DtmfTone.None)
                    {
                        _clientData = handleStart(tone);
                        _currentState = State.Dtmf;
                    }
                    break;
                case State.Dtmf:
                    if (tone == DtmfTone.None)
                    {
                        handleEnd((T)_clientData!, _lastTone);
                        _clientData = null;
                        _currentState = State.NoDtmf;
                    }
                    else if (tone != _lastTone)
                    {
                        handleEnd((T)_clientData!, _lastTone);
                        _clientData = handleStart(tone);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _lastTone = tone;
        }
    }
}