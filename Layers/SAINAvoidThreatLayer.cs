﻿using BepInEx.Logging;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using System.Text;
using SAIN.SAINComponent;
using SAIN.Layers.Combat.Solo.Cover;
using System.Collections.Generic;
using SAIN.Layers.Combat.Solo;

namespace SAIN.Layers
{
    internal class SAINAvoidThreatLayer : SAINLayer
    {
        public SAINAvoidThreatLayer(BotOwner bot, int priority) : base(bot, priority, Name)
        {
        }

        public static readonly string Name = BuildLayerName<SAINAvoidThreatLayer>();

        public override Action GetNextAction()
        {
            _lastActionDecision = CurrentDecision;
            switch (_lastActionDecision)
            {
                case SoloDecision.DogFight:
                    return new Action(typeof(DogFightAction), $"Dog Fight - Enemy Close!");

                case SoloDecision.AvoidGrenade:
                    return new Action(typeof(RunToCoverAction), $"Avoid Grenade");

                default:
                    return new Action(typeof(DogFightAction), $"NO DECISION - ERROR IN LOGIC");
            }
        }

        public override bool IsActive()
        {
            if (Bot == null) return false;
            SoloDecision decision = CurrentDecision;
            return decision == SoloDecision.DogFight || decision == SoloDecision.AvoidGrenade;
        }

        public override bool IsCurrentActionEnding()
        {
            if (Bot == null) return true;
            return _lastActionDecision != CurrentDecision;
        }

        private SoloDecision _lastActionDecision;
        public SoloDecision CurrentDecision => Bot.Decision.CurrentSoloDecision;
    }
}