﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.Coyote;
using Microsoft.Coyote.Actors;

namespace Coyote.Examples.ReplicatingStorage
{
    internal class RepairTimer : StateMachine
    {
        internal class ConfigureEvent : Event
        {
            public ActorId Target;

            public ConfigureEvent(ActorId id)
                : base()
            {
                this.Target = id;
            }
        }

        internal class StartTimerEvent : Event { }

        internal class CancelTimerEvent : Event { }

        internal class TimeoutEvent : Event { }

        private class TickEvent : Event { }

        private ActorId Target;

        [Start]
        [OnEventDoAction(typeof(ConfigureEvent), nameof(Configure))]
        [OnEventGotoState(typeof(StartTimerEvent), typeof(Active))]
        private class Init : State { }

        private Transition Configure(Event e)
        {
            this.Target = (e as ConfigureEvent).Target;
            return this.RaiseEvent(new StartTimerEvent());
        }

        [OnEntry(nameof(ActiveOnEntry))]
        [OnEventDoAction(typeof(TickEvent), nameof(Tick))]
        [OnEventGotoState(typeof(CancelTimerEvent), typeof(Inactive))]
        [IgnoreEvents(typeof(StartTimerEvent))]
        private class Active : State { }

        private void ActiveOnEntry()
        {
            this.SendEvent(this.Id, new TickEvent());
        }

        private void Tick()
        {
            if (this.Random())
            {
                this.Logger.WriteLine("\n [RepairTimer] " + this.Target + " | timed out\n");
                this.SendEvent(this.Target, new TimeoutEvent());
            }

            this.SendEvent(this.Id, new TickEvent());
        }

        [OnEventGotoState(typeof(StartTimerEvent), typeof(Active))]
        [IgnoreEvents(typeof(CancelTimerEvent), typeof(TickEvent))]
        private class Inactive : State { }
    }
}
