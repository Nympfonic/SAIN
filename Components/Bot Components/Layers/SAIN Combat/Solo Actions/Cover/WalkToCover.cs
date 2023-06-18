﻿using BepInEx.Logging;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Classes;
using SAIN.Classes.CombatFunctions;
using SAIN.Components;
using UnityEngine;

namespace SAIN.Layers
{
    internal class WalkToCover : CustomLogic
    {
        public WalkToCover(BotOwner bot) : base(bot)
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource(this.GetType().Name);
            SAIN = bot.GetComponent<SAINComponent>();
            Shoot = new ShootClass(bot);
        }

        private readonly ManualLogSource Logger;

        public override void Update()
        {
            if (CoverDestination != null && CoverDestination.BotIsHere)
            {
                EngageEnemy();
                return;
            }

            SAIN.Mover.SetTargetMoveSpeed(1f);
            SAIN.Mover.SetTargetPose(1f);

            FindTargetCover();

            if (CoverDestination == null)
            {
                EngageEnemy();
                return;
            }

            if (CoverDestination != null)
            {
                if (SAIN.Mover.Prone.ShallProne(CoverDestination, true) || SAIN.Mover.Prone.IsProne)
                {
                    SAIN.Mover.Prone.SetProne(true);
                    SAIN.Mover.StopMove();
                }
                else
                {
                    MoveTo(DestinationPosition);
                }

                EngageEnemy();
            }
        }

        private bool FindTargetCover()
        {
            var coverPoint = SAIN.Cover.ClosestPoint;
            if (coverPoint != null && !coverPoint.Spotted)
            {
                if (CanMoveTo(coverPoint, out Vector3 pointToGo))
                {
                    DestinationPosition = pointToGo;
                    return true;
                }
                else
                {
                    coverPoint.Spotted = true;
                }
            }
            if (CoverDestination != null)
            {
                CoverDestination.BotIsUsingThis = false;
            }
            return false;
        }

        private void MoveTo(Vector3 position)
        {
            CoverDestination.BotIsUsingThis = true;
            SAIN.Mover.GoToPoint(position);
            SAIN.Mover.SetTargetMoveSpeed(1f);
            SAIN.Mover.SetTargetPose(1f);
            BotOwner.DoorOpener.Update();
        }

        private bool CanMoveTo(CoverPoint coverPoint, out Vector3 pointToGo)
        {
            if (coverPoint != null && SAIN.Mover.CanGoToPoint(coverPoint.Position, out pointToGo))
            {
                return true;
            }
            pointToGo = Vector3.zero;
            return false;
        }

        private CoverPoint CoverDestination => SAIN.Cover.ClosestPoint;
        private Vector3 DestinationPosition;

        private void EngageEnemy()
        {
            SAIN.Steering.LookToMovingDirection(false);
            if (!SAIN.Steering.SteerByPriority(false))
            {
                if (SAIN.Enemy != null)
                {
                    SAIN.Steering.LookToEnemy(SAIN.Enemy);
                }
            }
            Shoot.Update();
        }

        private readonly ShootClass Shoot;

        public override void Start()
        {
        }

        public override void Stop()
        {
        }

        private readonly SAINComponent SAIN;
    }
}