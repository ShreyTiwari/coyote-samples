﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Coyote;
using Microsoft.Coyote.Actors;
using Microsoft.Coyote.Specifications;

namespace Coyote.Examples.TwoPhaseCommit
{
    internal class SafetyMonitor : Monitor
    {
        internal class Config : Event
        {
            public ActorId Coordinator;

            public Config(ActorId coordinator)
                : base()
            {
                this.Coordinator = coordinator;
            }
        }

        internal class MonitorWrite : Event
        {
            public int Idx;
            public int Val;

            public MonitorWrite(int idx, int val)
                : base()
            {
                this.Idx = idx;
                this.Val = val;
            }
        }

        internal class MonitorReadSuccess : Event
        {
            public int Idx;
            public int Val;

            public MonitorReadSuccess(int idx, int val)
                : base()
            {
                this.Idx = idx;
                this.Val = val;
            }
        }

        internal class MonitorReadUnavailable : Event
        {
            public int Idx;

            public MonitorReadUnavailable(int idx)
                : base()
            {
                this.Idx = idx;
            }
        }

        private class Unit : Event { }

        private Dictionary<int, int> Data;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        [OnEventDoAction(typeof(MonitorWrite), nameof(MonitorWriteAction))]
        [OnEventDoAction(typeof(MonitorReadSuccess), nameof(MonitorReadSuccessAction))]
        [OnEventDoAction(typeof(MonitorReadUnavailable), nameof(MonitorReadUnavailableAction))]
        private class Init : State { }

        private void InitOnEntry()
        {
            this.Data = new Dictionary<int, int>();
        }

        private void MonitorWriteAction(Event e)
        {
            var idx = (e as MonitorWrite).Idx;
            var val = (e as MonitorWrite).Val;

            if (!this.Data.ContainsKey(idx))
            {
                this.Data.Add(idx, val);
            }
            else
            {
                this.Data[idx] = val;
            }
        }

        private void MonitorReadSuccessAction(Event e)
        {
            var idx = (e as MonitorReadSuccess).Idx;
            var val = (e as MonitorReadSuccess).Val;
            this.Assert(this.Data.ContainsKey(idx));
            this.Assert(this.Data[idx] == val);
        }

        private void MonitorReadUnavailableAction(Event e)
        {
            var idx = (e as MonitorReadUnavailable).Idx;
            this.Assert(!this.Data.ContainsKey(idx));
        }
    }
}
