using System;

namespace KhimTools.Core.Preview
{
    public enum PreviewLifecycleState
    {
        NotGenerated,
        Generating,
        Valid,
        Stale,
        Invalid,
        HostVerificationRequired
    }

    /// <summary>Small UI-thread-owned lifecycle guard for detached preview payloads.</summary>
    public sealed class PreviewLifecycleSession<T> where T : class
    {
        public PreviewLifecycleState State { get; private set; }
        public string InputFingerprint { get; private set; }
        public T Payload { get; private set; }

        public PreviewLifecycleSession()
        {
            State = PreviewLifecycleState.NotGenerated;
            InputFingerprint = string.Empty;
        }

        public void BeginGeneration()
        {
            Payload = null;
            InputFingerprint = string.Empty;
            State = PreviewLifecycleState.Generating;
        }

        public void Complete(T payload, string inputFingerprint)
        {
            if (State != PreviewLifecycleState.Generating)
                throw new InvalidOperationException("Preview completion requires an active generation state.");
            if (payload == null) throw new ArgumentNullException("payload");
            if (string.IsNullOrWhiteSpace(inputFingerprint))
                throw new ArgumentException("A deterministic input fingerprint is required.", "inputFingerprint");
            Payload = payload;
            InputFingerprint = inputFingerprint;
            State = PreviewLifecycleState.Valid;
        }

        public bool TryGetValid(string currentInputFingerprint, out T payload)
        {
            payload = null;
            if (State != PreviewLifecycleState.Valid || Payload == null) return false;
            if (!string.Equals(InputFingerprint, currentInputFingerprint, StringComparison.Ordinal))
            {
                MarkStale();
                return false;
            }
            payload = Payload;
            return true;
        }

        public void MarkStale()
        {
            Payload = null;
            State = PreviewLifecycleState.Stale;
        }

        public void Invalidate()
        {
            Payload = null;
            InputFingerprint = string.Empty;
            State = PreviewLifecycleState.Invalid;
        }

        public void RequireHostVerification()
        {
            Payload = null;
            State = PreviewLifecycleState.HostVerificationRequired;
        }
    }
}
