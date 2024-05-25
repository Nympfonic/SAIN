using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Interpolation;
using SAIN.BotController.Classes;
using SAIN.Components.BotController;
using SAIN.Helpers;
using SAIN.Preset.GlobalSettings.Categories;
using SAIN.SAINComponent;
using SAIN.SAINComponent.Classes.Enemy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using static RootMotion.FinalIK.AimPoser;
using static RootMotion.FinalIK.InteractionTrigger;

namespace SAIN.Components
{
    public class SAINBotController : MonoBehaviour
    {
        public static SAINBotController Instance;
        public SAINBotController()
        {
            Instance = this;
        }

        public Action<SAINSoundType, Vector3, Player, float> AISoundPlayed { get; set; }
        public Action<EPhraseTrigger, ETagStatus, Player> PlayerTalk { get; set; }
        public Action<Vector3> BulletImpact { get; set; }

        public Dictionary<string, BotComponent> Bots => BotSpawnController.Bots;
        public GameWorld GameWorld => Singleton<GameWorld>.Instance;
        public IBotGame BotGame => Singleton<IBotGame>.Instance;
        public Player MainPlayer => Singleton<GameWorld>.Instance?.MainPlayer;

        public BotsController DefaultController { get; set; }
        public BotSpawner BotSpawner { get; set; }
        public CoverManager CoverManager { get; private set; } = new CoverManager();
        public LineOfSightManager LineOfSightManager { get; private set; } = new LineOfSightManager();
        public BotExtractManager BotExtractManager { get; private set; } = new BotExtractManager();
        public TimeClass TimeVision { get; private set; } = new TimeClass();
        public WeatherVisionClass WeatherVision { get; private set; } = new WeatherVisionClass();
        public BotSpawnController BotSpawnController { get; private set; } = new BotSpawnController();
        public BotSquads BotSquads { get; private set; } = new BotSquads();

        private void Awake()
        {
            GameWorld.OnDispose += Dispose;

            BotSpawnController.Awake();
            TimeVision.Awake();
            LineOfSightManager.Awake();
            CoverManager.Awake();
            PathManager.Awake();
            BotExtractManager.Awake();
            BotSquads.Awake();

            Singleton<BotEventHandler>.Instance.OnGrenadeThrow += GrenadeThrown;
            Singleton<BotEventHandler>.Instance.OnGrenadeExplosive += GrenadeExplosion;
            AISoundPlayed += SoundPlayed;
            PlayerTalk += PlayerTalked;
        }

        private void Update()
        {
            if (GameWorld == null)
            {
                Dispose();
                return;
            }
            if (BotGame == null)
            {
                return;
            }

            BotSquads.Update();
            BotSpawnController.Update();
            BotExtractManager.Update();
            TimeVision.Update();
            WeatherVision.Update();
            LineOfSightManager.Update();

            //CoverManager.Update();
            //PathManager.Update();
            //AddNavObstacles();
            //UpdateObstacles();
        }

        public IEnumerator PlayShootSoundCoroutine(Player player)
        {
            yield return null;
            AudioHelpers.TryPlayShootSound(player);
        }

        private void PlayerTalked(EPhraseTrigger phrase, ETagStatus mask, Player player)
        {
            if (player == null || Bots == null)
            {
                return;
            }
            foreach (var bot in Bots)
            {
                BotComponent sain = bot.Value;
                if (sain != null && sain.BotOwner != null && bot.Key != player.ProfileId)
                {
                    if (sain.BotOwner.BotsGroup.IsPlayerEnemyByRole(player.Profile.Info.Settings.Role))
                    {
                        sain.Talk.EnemyTalk.SetEnemyTalk(player);
                    }
                    else
                    {
                        sain.Talk.EnemyTalk.SetFriendlyTalked(player);
                    }
                }
            }
        }

        public void BotDeath(BotOwner bot)
        {
            if (bot?.GetPlayer != null && bot.IsDead)
            {
                DeadBots.Add(bot.GetPlayer);
            }
        }

        public List<Player> DeadBots { get; private set; } = new List<Player>();
        public List<BotDeathObject> DeathObstacles { get; private set; } = new List<BotDeathObject>();

        private readonly List<int> IndexToRemove = new List<int>();

        public void AddNavObstacles()
        {
            if (DeadBots.Count > 0)
            {
                const float ObstacleRadius = 1.5f;

                for (int i = 0; i < DeadBots.Count; i++)
                {
                    var bot = DeadBots[i];
                    if (bot == null || bot.GetPlayer == null)
                    {
                        IndexToRemove.Add(i);
                        continue;
                    }
                    bool enableObstacle = true;
                    Collider[] players = Physics.OverlapSphere(bot.Position, ObstacleRadius, LayerMaskClass.PlayerMask);
                    foreach (var p in players)
                    {
                        if (p == null) continue;
                        if (p.TryGetComponent<Player>(out var player))
                        {
                            if (player.IsAI && player.HealthController.IsAlive)
                            {
                                enableObstacle = false;
                                break;
                            }
                        }
                    }
                    if (enableObstacle)
                    {
                        if (bot != null && bot.GetPlayer != null)
                        {
                            var obstacle = new BotDeathObject(bot);
                            obstacle.Activate(ObstacleRadius);
                            DeathObstacles.Add(obstacle);
                        }
                        IndexToRemove.Add(i);
                    }
                }

                foreach (var index in IndexToRemove)
                {
                    DeadBots.RemoveAt(index);
                }

                IndexToRemove.Clear();
            }
        }

        private void UpdateObstacles()
        {
            if (DeathObstacles.Count > 0)
            {
                for (int i = 0; i < DeathObstacles.Count; i++)
                {
                    var obstacle = DeathObstacles[i];
                    if (obstacle?.TimeSinceCreated > 30f)
                    {
                        obstacle?.Dispose();
                        IndexToRemove.Add(i);
                    }
                }

                foreach (var index in IndexToRemove)
                {
                    DeathObstacles.RemoveAt(index);
                }

                IndexToRemove.Clear();
            }
        }

        public void SoundPlayed(SAINSoundType soundType, Vector3 position, Player player, float range)
        {
            if (Bots.Count == 0 || player == null)
            {
                return;
            }

            Singleton<BotEventHandler>.Instance?.PlaySound(player, player.Position, range, AISoundType.step);
            StartCoroutine(delayHearAction(player, range, soundType));
        }

        private IEnumerator delayHearAction(Player player, float range, SAINSoundType soundType, float delay = 0.25f)
        {
            yield return new WaitForSeconds(delay);

            if (player == null || !player.HealthController.IsAlive)
            {
                yield break;
            }
            foreach (var bot in Bots.Values)
            {
                if (bot == null || player.ProfileId == bot.Player.ProfileId)
                {
                    continue;
                }

                SAINEnemy Enemy = bot.EnemyController.GetEnemy(player.ProfileId);
                if (Enemy?.EnemyPerson.IsActive == true 
                    && Enemy.IsValid 
                    && Enemy.RealDistance <= range)
                {
                    bool shallUpdateSquad = true;
                    if (soundType == SAINSoundType.GrenadePin || soundType == SAINSoundType.GrenadeDraw)
                    {
                        Enemy.EnemyStatus.EnemyHasGrenadeOut = true;
                    }
                    else if (soundType == SAINSoundType.Reload)
                    {
                        Enemy.EnemyStatus.EnemyIsReloading = true;
                    }
                    else if (soundType == SAINSoundType.Looting)
                    {
                        Enemy.EnemyStatus.EnemyIsLooting = true;
                    }
                    else if (soundType == SAINSoundType.Heal)
                    {
                        Enemy.EnemyStatus.EnemyIsHealing = true;
                    }
                    else if (soundType == SAINSoundType.Surgery)
                    {
                        Enemy.EnemyStatus.VulnerableAction = SAINComponent.Classes.Enemy.EEnemyAction.UsingSurgery;
                    }
                    else
                    {
                        shallUpdateSquad = false;
                    }

                    if (shallUpdateSquad)
                    {
                        bot.Squad.SquadInfo.UpdateSharedEnemyStatus(Enemy.EnemyIPlayer, Enemy.EnemyStatus.VulnerableAction, bot);
                    }
                }
            }
        }

        private void GrenadeExplosion(Vector3 explosionPosition, string playerProfileID, bool isSmoke, float smokeRadius, float smokeLifeTime)
        {
            if (!Singleton<BotEventHandler>.Instantiated || playerProfileID == null)
            {
                return;
            }
            Player player = EFTInfo.GetPlayer(playerProfileID);
            if (player != null)
            {
                if (!isSmoke)
                {
                    registerGrenadeExplosionForSAINBots(explosionPosition, player, playerProfileID, 200f);
                }
                else
                {
                    registerGrenadeExplosionForSAINBots(explosionPosition, player, playerProfileID, 50f); 

                    float radius = smokeRadius * HelpersGClass.SMOKE_GRENADE_RADIUS_COEF;
                    Vector3 position = player.Position;
                    foreach (var keyValuePair in DefaultController.Groups())
                    {
                        foreach (BotsGroup botGroupClass in keyValuePair.Value.GetGroups(true))
                        {
                            botGroupClass.AddSmokePlace(explosionPosition, smokeLifeTime, radius, position);
                        }
                    }
                }
            }
        }

        private void registerGrenadeExplosionForSAINBots(Vector3 explosionPosition, Player player, string playerProfileID, float range)
        {
            // Play a sound with the input range.
            Singleton<BotEventHandler>.Instance?.PlaySound(player, explosionPosition, range, AISoundType.gun);

            // We dont want bots to think the grenade explosion was a place they heard an enemy, so set this manually.
            foreach (var bot in Bots.Values)
            {
                if (bot != null)
                {
                    float distance = (bot.Position - explosionPosition).magnitude;
                    if (distance < range)
                    {
                        SAINEnemy enemy = bot.EnemyController.GetEnemy(playerProfileID);
                        if (enemy != null)
                        {
                            float dispersion = distance / 10f;
                            Vector3 random = UnityEngine.Random.onUnitSphere * dispersion;
                            random.y = 0;
                            Vector3 estimatedThrowPosition = enemy.EnemyPosition + random;
                            enemy.SetHeardStatus(true, estimatedThrowPosition, SAINSoundType.GrenadeExplosion);
                        }
                    }
                }
            }
        }

        private void GrenadeThrown(Grenade grenade, Vector3 position, Vector3 force, float mass)
        {
            if (grenade != null)
            {
                StartCoroutine(grenadeThrown(grenade, position, force, mass));
            }
        }

        private IEnumerator grenadeThrown(Grenade grenade, Vector3 position, Vector3 force, float mass)
        {
            var danger = Vector.DangerPoint(position, force, mass);
            yield return null;
            Player player = EFTInfo.GetPlayer(grenade.ProfileId);
            if (player == null)
            {
                Logger.LogError($"Player Null from ID {grenade.ProfileId}");
                yield break;
            }
            if (player.HealthController.IsAlive)
            {
                foreach (var bot in Bots.Values)
                {
                    if (bot?.BotActive == true &&
                        !bot.EnemyController.IsPlayerFriendly(player) &&
                        (danger - bot.Position).sqrMagnitude < 100f * 100f)
                    {
                        bot.Grenade.EnemyGrenadeThrown(grenade, danger);
                    }
                }
            }
            yield return null;
        }

        public List<string> Groups = new List<string>();
        public PathManager PathManager { get; private set; } = new PathManager();

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            try
            {
                StopAllCoroutines();

                GameWorld.OnDispose -= Dispose;

                Patches.Vision.AIVisionUpdateLimitPatch.LookUpdates.Clear();

                AISoundPlayed -= SoundPlayed;
                PlayerTalk -= PlayerTalked;
                Singleton<BotEventHandler>.Instance.OnGrenadeThrow -= GrenadeThrown;
                Singleton<BotEventHandler>.Instance.OnGrenadeExplosive -= GrenadeExplosion;

                if (Bots.Count > 0)
                {
                    foreach (var bot in Bots)
                    {
                        bot.Value?.Dispose();
                    }
                }
                Bots.Clear();
                Destroy(this);
            }
            catch { }
        }

        public bool GetSAIN(string botName, out BotComponent bot)
        {
            StringBuilder debugString = null;
            bot = BotSpawnController.GetSAIN(botName, debugString);
            return bot != null;
        }

        public bool GetSAIN(BotOwner botOwner, out BotComponent bot)
        {
            StringBuilder debugString = null;
            bot = BotSpawnController.GetSAIN(botOwner, debugString);
            return bot != null;
        }

        public bool GetSAIN(Player player, out BotComponent bot)
        {
            StringBuilder debugString = null;
            bot = BotSpawnController.GetSAIN(player, debugString);
            return bot != null;
        }
    }

    public class BotDeathObject
    {
        public BotDeathObject(Player player)
        {
            Player = player;
            NavMeshObstacle = player.gameObject.AddComponent<NavMeshObstacle>();
            NavMeshObstacle.carving = false;
            NavMeshObstacle.enabled = false;
            Position = player.Position;
            TimeCreated = Time.time;
        }

        public void Activate(float radius = 2f)
        {
            if (NavMeshObstacle != null)
            {
                NavMeshObstacle.enabled = true;
                NavMeshObstacle.carving = true;
                NavMeshObstacle.radius = radius;
            }
        }

        public void Dispose()
        {
            if (NavMeshObstacle != null)
            {
                NavMeshObstacle.carving = false;
                NavMeshObstacle.enabled = false;
                GameObject.Destroy(NavMeshObstacle);
            }
        }

        public NavMeshObstacle NavMeshObstacle { get; private set; }
        public Player Player { get; private set; }
        public Vector3 Position { get; private set; }
        public float TimeCreated { get; private set; }
        public float TimeSinceCreated => Time.time - TimeCreated;
        public bool ObstacleActive => NavMeshObstacle.carving;
    }
}