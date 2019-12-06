﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Coyote;
using Microsoft.Coyote.Actors;

namespace Coyote.Examples.ChainReplication
{
    internal class ChainReplicationMaster : StateMachine
    {
        internal class Config : Event
        {
            public List<ActorId> Servers;
            public List<ActorId> Clients;

            public Config(List<ActorId> servers, List<ActorId> clients)
                : base()
            {
                this.Servers = servers;
                this.Clients = clients;
            }
        }

        internal class BecomeHead : Event
        {
            public ActorId Target;

            public BecomeHead(ActorId target)
                : base()
            {
                this.Target = target;
            }
        }

        internal class BecomeTail : Event
        {
            public ActorId Target;

            public BecomeTail(ActorId target)
                : base()
            {
                this.Target = target;
            }
        }

        internal class Success : Event { }

        internal class HeadChanged : Event { }

        internal class TailChanged : Event { }

        private class HeadFailed : Event { }

        private class TailFailed : Event { }

        private class ServerFailed : Event { }

        private class FixSuccessor : Event { }

        private class FixPredecessor : Event { }

        private class Local : Event { }

        private class Done : Event { }

        private List<ActorId> Servers;
        private List<ActorId> Clients;

        private ActorId FailureDetector;

        private ActorId Head;
        private ActorId Tail;

        private int FaultyNodeIndex;
        private int LastUpdateReceivedSucc;
        private int LastAckSent;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        [OnEventGotoState(typeof(Local), typeof(WaitForFailure))]
        private class Init : State { }

        private Transition InitOnEntry(Event e)
        {
            this.Servers = (e as Config).Servers;
            this.Clients = (e as Config).Clients;

            this.FailureDetector = this.CreateActor(typeof(FailureDetector),
                new FailureDetector.Config(this.Id, this.Servers));

            this.Head = this.Servers[0];
            this.Tail = this.Servers[this.Servers.Count - 1];

            return this.RaiseEvent(new Local());
        }

        [OnEventGotoState(typeof(HeadFailed), typeof(CorrectHeadFailure))]
        [OnEventGotoState(typeof(TailFailed), typeof(CorrectTailFailure))]
        [OnEventGotoState(typeof(ServerFailed), typeof(CorrectServerFailure))]
        [OnEventDoAction(typeof(FailureDetector.FailureDetected), nameof(CheckWhichNodeFailed))]
        private class WaitForFailure : State { }

        private Transition CheckWhichNodeFailed(Event e)
        {
            this.Assert(this.Servers.Count > 1, "All nodes have failed.");

            var failedServer = (e as FailureDetector.FailureDetected).Server;

            if (this.Head.Equals(failedServer))
            {
                return this.RaiseEvent(new HeadFailed());
            }
            else if (this.Tail.Equals(failedServer))
            {
                return this.RaiseEvent(new TailFailed());
            }

            for (int i = 0; i < this.Servers.Count - 1; i++)
            {
                if (this.Servers[i].Equals(failedServer))
                {
                    this.FaultyNodeIndex = i;
                }
            }

            return this.RaiseEvent(new ServerFailed());
        }

        [OnEntry(nameof(CorrectHeadFailureOnEntry))]
        [OnEventGotoState(typeof(Done), typeof(WaitForFailure), nameof(UpdateFailureDetector))]
        [OnEventDoAction(typeof(HeadChanged), nameof(UpdateClients))]
        private class CorrectHeadFailure : State { }

        private void CorrectHeadFailureOnEntry()
        {
            this.Servers.RemoveAt(0);

            this.Monitor<InvariantMonitor>(
                new InvariantMonitor.UpdateServers(this.Servers));
            this.Monitor<ServerResponseSeqMonitor>(
                new ServerResponseSeqMonitor.UpdateServers(this.Servers));

            this.Head = this.Servers[0];

            this.SendEvent(this.Head, new BecomeHead(this.Id));
        }

        private Transition UpdateClients()
        {
            for (int i = 0; i < this.Clients.Count; i++)
            {
                this.SendEvent(this.Clients[i], new Client.UpdateHeadTail(this.Head, this.Tail));
            }

            return this.RaiseEvent(new Done());
        }

        private void UpdateFailureDetector()
        {
            this.SendEvent(this.FailureDetector, new FailureDetector.FailureCorrected(this.Servers));
        }

        [OnEntry(nameof(CorrectTailFailureOnEntry))]
        [OnEventGotoState(typeof(Done), typeof(WaitForFailure), nameof(UpdateFailureDetector))]
        [OnEventDoAction(typeof(TailChanged), nameof(UpdateClients))]
        private class CorrectTailFailure : State { }

        private void CorrectTailFailureOnEntry()
        {
            this.Servers.RemoveAt(this.Servers.Count - 1);

            this.Monitor<InvariantMonitor>(
                new InvariantMonitor.UpdateServers(this.Servers));
            this.Monitor<ServerResponseSeqMonitor>(
                new ServerResponseSeqMonitor.UpdateServers(this.Servers));

            this.Tail = this.Servers[this.Servers.Count - 1];

            this.SendEvent(this.Tail, new BecomeTail(this.Id));
        }

        [OnEntry(nameof(CorrectServerFailureOnEntry))]
        [OnEventGotoState(typeof(Done), typeof(WaitForFailure), nameof(UpdateFailureDetector))]
        [OnEventDoAction(typeof(FixSuccessor), nameof(UpdateClients))]
        [OnEventDoAction(typeof(FixPredecessor), nameof(ProcessFixPredecessor))]
        [OnEventDoAction(typeof(ChainReplicationServer.NewSuccInfo), nameof(SetLastUpdate))]
        [OnEventDoAction(typeof(Success), nameof(ProcessSuccess))]
        private class CorrectServerFailure : State { }

        private Transition CorrectServerFailureOnEntry()
        {
            this.Servers.RemoveAt(this.FaultyNodeIndex);

            this.Monitor<InvariantMonitor>(
                new InvariantMonitor.UpdateServers(this.Servers));
            this.Monitor<ServerResponseSeqMonitor>(
                new ServerResponseSeqMonitor.UpdateServers(this.Servers));

            return this.RaiseEvent(new FixSuccessor());
        }

        private void ProcessFixPredecessor()
        {
            this.SendEvent(this.Servers[this.FaultyNodeIndex - 1], new ChainReplicationServer.NewSuccessor(this.Id,
                this.Servers[this.FaultyNodeIndex], this.LastAckSent, this.LastUpdateReceivedSucc));
        }

        private Transition SetLastUpdate(Event e)
        {
            this.LastUpdateReceivedSucc = (e as ChainReplicationServer.NewSuccInfo).LastUpdateReceivedSucc;
            this.LastAckSent = (e as ChainReplicationServer.NewSuccInfo).LastAckSent;
            return this.RaiseEvent(new FixPredecessor());
        }

        private Transition ProcessSuccess()
        {
            return this.RaiseEvent(new Done());
        }
    }
}
