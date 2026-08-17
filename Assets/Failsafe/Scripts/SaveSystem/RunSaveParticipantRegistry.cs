using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Failsafe.Scripts.SaveSystem
{
    public sealed class RunSaveParticipantRegistry
    {
        private readonly List<IRunSaveParticipant> _participants = new List<IRunSaveParticipant>();

        public int Count => _participants.Count;

        public bool IsRegistered(string participantId)
        {
            if (string.IsNullOrWhiteSpace(participantId))
                return false;

            for (int i = 0; i < _participants.Count; i++)
            {
                if (string.Equals(
                        _participants[i].ParticipantId,
                        participantId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public IDisposable Register(IRunSaveParticipant participant)
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));

            if (string.IsNullOrWhiteSpace(participant.ParticipantId))
                throw new ArgumentException("Save participant must have a non-empty id.", nameof(participant));

            for (int i = 0; i < _participants.Count; i++)
            {
                IRunSaveParticipant registered = _participants[i];

                if (ReferenceEquals(registered, participant))
                    return Registration.Empty;

                if (string.Equals(registered.ParticipantId, participant.ParticipantId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"A save participant with id '{participant.ParticipantId}' is already registered.");
                }
            }

            _participants.Add(participant);
            return new Registration(this, participant);
        }

        public void Unregister(IRunSaveParticipant participant)
        {
            if (participant != null)
                _participants.Remove(participant);
        }

        public void CaptureAll(RunCheckpointData checkpoint)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            List<IRunSaveParticipant> snapshot = CreateOrderedSnapshot();
            for (int i = 0; i < snapshot.Count; i++)
                snapshot[i].Capture(checkpoint);
        }

        public async UniTask RestoreAllAsync(RunCheckpointData checkpoint, RunLoadContext context)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            List<IRunSaveParticipant> snapshot = CreateOrderedSnapshot();
            for (int i = 0; i < snapshot.Count; i++)
                await snapshot[i].RestoreAsync(checkpoint, context);
        }

        private List<IRunSaveParticipant> CreateOrderedSnapshot()
        {
            List<IRunSaveParticipant> snapshot = new List<IRunSaveParticipant>(_participants);
            snapshot.Sort(CompareParticipants);
            return snapshot;
        }

        private static int CompareParticipants(IRunSaveParticipant left, IRunSaveParticipant right)
        {
            int orderComparison = left.RestoreOrder.CompareTo(right.RestoreOrder);
            return orderComparison != 0
                ? orderComparison
                : string.CompareOrdinal(left.ParticipantId, right.ParticipantId);
        }

        private sealed class Registration : IDisposable
        {
            public static readonly IDisposable Empty = new Registration();

            private RunSaveParticipantRegistry _registry;
            private IRunSaveParticipant _participant;

            private Registration()
            {
            }

            public Registration(RunSaveParticipantRegistry registry, IRunSaveParticipant participant)
            {
                _registry = registry;
                _participant = participant;
            }

            public void Dispose()
            {
                if (_registry == null)
                    return;

                _registry.Unregister(_participant);
                _registry = null;
                _participant = null;
            }
        }
    }
}
