﻿using EFT;
using SAIN.Components;
using UnityEngine;

namespace SAIN.Classes.CombatFunctions
{
    public class ShootClass : GClass104
    {
        public ShootClass(BotOwner owner, SAINComponent sain) : base(owner)
        {
            SAIN = sain;
            FriendlyFire = new FriendlyFireClass(owner);
            BotShoot = new GClass182(owner);
        }

        private readonly GClass182 BotShoot;
        private BotOwner BotOwner => botOwner_0;

        private SAINComponent SAIN;

        public override void Update()
        {
            FriendlyFire.Update();

            var enemy = BotOwner.Memory.GoalEnemy;
            if (enemy != null && enemy.CanShoot && enemy.IsVisible)
            {
                if (AimingData == null)
                {
                    AimingData = BotOwner.AimingData;
                }

                Vector3? pointToShoot = GetPointToShoot();
                if (pointToShoot != null)
                {
                    BotOwner.BotLight?.TurnOn(true);
                    Target = pointToShoot.Value;
                    if (AimingData.IsReady)
                    {
                        ReadyToShoot();
                        BotShoot.Update();
                    }
                }
            }
            else
            {
                BotOwner.BotLight?.TurnOff(true, true);
            }
        }

        protected virtual void ReadyToShoot()
        {
        }

        protected virtual Vector3? GetTarget()
        {
            var enemy = BotOwner.Memory.GoalEnemy;
            if (enemy != null && enemy.CanShoot && enemy.IsVisible)
            {
                Vector3 value;
                if (enemy.Distance < botOwner_0.Settings.FileSettings.Aiming.DIST_TO_SHOOT_TO_CENTER)
                {
                    value = enemy.GetCenterPart();
                }
                else
                {
                    value = enemy.GetPartToShoot();
                }
                return new Vector3?(value);
            }
            Vector3? result = null;
            if (BotOwner.Memory.LastEnemy != null)
            {
                result = new Vector3?(BotOwner.Memory.LastEnemy.CurrPosition + Vector3.up * BotOwner.Settings.FileSettings.Aiming.DANGER_UP_POINT);
            }
            return result;
        }

        protected virtual Vector3? GetPointToShoot()
        {
            Vector3? target = GetTarget();
            if (target != null)
            {
                Target = target.Value;
                AimingData.SetTarget(Target);
                AimingData.NodeUpdate();
                return new Vector3?(Target);
            }
            return null;
        }

        protected Vector3 Target;
        private GInterface5 AimingData;

        public FriendlyFireClass FriendlyFire { get; private set; }
    }
}