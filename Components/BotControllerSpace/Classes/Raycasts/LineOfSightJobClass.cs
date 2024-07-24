using SAIN.SAINComponent;
using SAIN.SAINComponent.Classes.EnemyClasses;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace SAIN.Components
{
    //public struct RaycastEnemiesJob : IJobFor
    //{
    //    public NativeArray<EnemyRaycastStruct> Raycasts;

    //    public void Execute(int index)
    //    {
    //        EnemyRaycastStruct raycast = Raycasts[index];

    //        LayerMask lineOfSightMask = LayerMaskClass.HighPolyWithTerrainMask;
    //        LayerMask shootMask = LayerMaskClass.HighPolyWithTerrainMask;
    //        LayerMask visionMask = LayerMaskClass.AI;

    //        float enemyDistance = raycast.EnemyDistance;
    //        Vector3 eyePos = raycast.EyePosition;
    //        Vector3 shootPoint = raycast.WeaponFirePort;

    //        var parts = raycast.Raycasts;
    //        int count = parts.Length;

    //        for (int i = 0; i < count; i++) {
    //            parts[i] = checkPart(
    //                parts[i],
    //                enemyDistance,
    //                eyePos,
    //                shootPoint,
    //                lineOfSightMask,
    //                shootMask,
    //                visionMask);
    //        }
    //    }

    //    private BodyPartRaycast checkPart(BodyPartRaycast part, float enemyDistance, Vector3 eyePos, Vector3 shootPoint, LayerMask LOSMask, LayerMask shootMask, LayerMask visionMask)
    //    {
    //        part.LineOfSight = false;
    //        part.CanShoot = false;
    //        if (enemyDistance > part.MaxRange) {
    //            return part;
    //        }

    //        Vector3 castPoint = part.CastPoint;
    //        Vector3 direction = castPoint - eyePos;
    //        float distance = direction.magnitude;
    //        part.LineOfSight = checkLineOfSight(eyePos, direction, out RaycastHit losHit, distance, LOSMask);
    //        part.LOSRaycastHit = losHit;
    //        if (part.LineOfSight) {
    //            Vector3 weaponDirection = castPoint - shootPoint;
    //            float weapDist = weaponDirection.magnitude;
    //            part.CanShoot = checkShoot(shootPoint, weaponDirection, out RaycastHit shootHit, weapDist, shootMask);
    //            part.ShootRaycastHit = shootHit;

    //            //part.IsVisible = checkVisible(eyePos, direction, out RaycastHit visionHit, distance, visionMask);
    //            //part.VisionRaycastHit = visionHit;
    //        }
    //        return part;
    //    }

    //    private bool checkLineOfSight(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, LayerMask mask)
    //    {
    //        return !Physics.Raycast(origin, direction, out hit, distance, mask);
    //    }

    //    private bool checkShoot(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, LayerMask mask)
    //    {
    //        return !Physics.Raycast(origin, direction, out hit, distance, mask);
    //    }

    //    private bool checkVisible(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, LayerMask mask)
    //    {
    //        return !Physics.Raycast(origin, direction, out hit, distance, mask);
    //    }
    //}

    public struct EnemyRaycastStruct
    {
        public string BotName;
        public string EnemyProfileId;
        public Vector3 EyePosition;
        public Vector3 WeaponFirePort;
        public float EnemyDistance;
        //public NativeArray<BodyPartRaycast> Raycasts;
        public BodyPartRaycast[] Raycasts;
    }

    public struct BodyPartRaycast
    {
        public EBodyPart PartType;
        public EBodyPartColliderType ColliderType;

        public float MaxRange;
        public Vector3 CastPoint;

        public RaycastHit LOSRaycastHit;
        public bool LineOfSight;

        public RaycastHit ShootRaycastHit;
        public bool CanShoot;

        public RaycastHit VisionRaycastHit;
        public bool IsVisible;
    }

    public class LineOfSightJobClass : SAINControllerBase
    {
        private const int BOTS_PER_FRAME = 5;

        private bool _hasJobFromLastFrame = false;
        private bool _isLineOfSightJob = true;

        private JobHandle _raycastJobHandle;
        //private RaycastEnemiesJob _raycastJob;
        //private EnemyRaycastStruct[] _raycastArray;

        private NativeArray<RaycastCommand> _lineOfSightRaycastCommands;
        private NativeArray<RaycastHit> _lineOfSightRaycastHits;

        private NativeArray<RaycastCommand> _canShootRaycastCommands;
        private NativeArray<RaycastHit> _canShootRaycastHits;

        private readonly List<BotComponent> _localList = new List<BotComponent>();
        private readonly List<EnemyRaycastStruct> _enemyRaycasts = new List<EnemyRaycastStruct>();

        public LineOfSightJobClass(SAINBotController botController) : base(botController)
        {
            botController.BotSpawnController.OnBotRemoved += onBotRemoved;
        }

        public void Update()
        {
            //try {
            finishJob();
            if (Bots.Count == 0) {
                return;
            }
            setupJob();
            //}
            //catch (Exception ex) {
            //    Logger.LogError(ex);
            //}
        }

        public void Dispose()
        {
            BotController.BotSpawnController.OnBotRemoved -= onBotRemoved;
            if (_lineOfSightRaycastCommands.IsCreated) _lineOfSightRaycastCommands.Dispose();
            if (_lineOfSightRaycastHits.IsCreated) _lineOfSightRaycastHits.Dispose();
            if (_canShootRaycastCommands.IsCreated) _canShootRaycastCommands.Dispose();
            if (_canShootRaycastHits.IsCreated) _canShootRaycastHits.Dispose();
        }

        private void onBotRemoved(BotComponent bot)
        {
            //finishJob();
        }

        private void finishJob()
        {
            if (!_hasJobFromLastFrame) {
                return;
            }

            // Ensure the last frame's job is completed
            _raycastJobHandle.Complete();

            if (!_isLineOfSightJob) {
                updateBotLineOfSight();
                _canShootRaycastCommands.Dispose();
                _canShootRaycastHits.Dispose();
            }

            _isLineOfSightJob = !_isLineOfSightJob;
            _hasJobFromLastFrame = false;
        }

        private void updateBotLineOfSight()
        {
            int enemyCount = _enemyRaycasts.Count;

            for (int i = 0; i < enemyCount; i++) {
                var raycastStruct = _enemyRaycasts[i];
                if (!Bots.TryGetValue(raycastStruct.BotName, out var bot) || bot == null) {
                    continue;
                }

                bot.Vision.TimeLastCheckedLOS = Time.time;
                if (!bot.BotActive) {
                    continue;
                }

                Enemy enemy = bot.EnemyController.GetEnemy(raycastStruct.EnemyProfileId, false);
                if (enemy == null) {
                    continue;
                }

                int raycastCount = raycastStruct.Raycasts.Length;
                var enemyParts = enemy.Vision.VisionChecker.EnemyParts.Parts;

                for (int j = 0; j < raycastCount; j++) {
                    var raycastPart = raycastStruct.Raycasts[j];
                    enemyParts.TryGetValue(raycastPart.PartType, out var partData);
                    partData?.SetLineOfSight(raycastPart);
                }
            }
        }

        private void findBotsForJob()
        {
            _localList.Clear();
            _localList.AddRange(Bots.Values);
            _enemyRaycasts.Clear();
            findBotsToCheck(_localList, _enemyRaycasts, -1);
        }

        private void findBotsToCheck(List<BotComponent> bots, IList<EnemyRaycastStruct> enemiesResult, int countToCheck)
        {
            // sort bots by the time they were last run through this function,
            // the lower the TimeLastChecked, the longer the time since they had their enemies checked
            bots.Sort((x, y) => x.Vision.TimeLastCheckedLOS.CompareTo(y.Vision.TimeLastCheckedLOS));

            int foundBots = 0;
            for (int i = 0; i < bots.Count; i++) {
                BotComponent bot = bots[i];
                if (bot == null) continue;
                if (!bot.BotActive) continue;
                if (bot.Vision.TimeSinceCheckedLOS < 0.05f) continue;

                Vector3 origin = bot.Transform.EyePosition;
                Vector3 firePort = bot.Transform.WeaponFirePort;
                var enemies = bot.EnemyController.Enemies;
                bool gotEnemyToCheck = false;
                float time = Time.time;

                foreach (Enemy enemy in enemies.Values) {
                    if (enemy == null) continue;

                    float delay = enemy.IsAI ? 0.1f : 0.05f;
                    if (time - enemy.Vision.VisionChecker.LastCheckLOSTime < delay) continue;
                    if (!enemy.CheckValid()) continue;

                    List<BodyPartRaycast> raycasts = enemy.Vision.VisionChecker.GetPartsToCheck(origin);
                    if (raycasts.Count == 0) continue;

                    EnemyRaycastStruct result = new EnemyRaycastStruct {
                        BotName = bot.name,
                        EnemyProfileId = enemy.EnemyProfileId,
                        EnemyDistance = enemy.RealDistance,
                        EyePosition = origin,
                        WeaponFirePort = firePort,
                        Raycasts = raycasts.ToArray(),
                    };
                    enemiesResult.Add(result);
                    if (!gotEnemyToCheck)
                        gotEnemyToCheck = true;
                }

                if (countToCheck > 0 && gotEnemyToCheck) {
                    foundBots++;
                    if (foundBots == countToCheck) {
                        break;
                    }
                }
            }
        }

        private void setupJob()
		{
            if (_isLineOfSightJob) {
                findBotsForJob();

                int raycastCount = 0;
			    int enemyCount = _enemyRaycasts.Count;

			    for (int i = 0; i < enemyCount; i++)
				    raycastCount += _enemyRaycasts[i].Raycasts.Length;

                setupLineOfSightJob(_enemyRaycasts, enemyCount, raycastCount);
            }
            else {
                setupCanShootJob(_enemyRaycasts, _enemyRaycasts.Count);
            }
            
            _hasJobFromLastFrame = true;
		}

		private void setupLineOfSightJob(IList<EnemyRaycastStruct> enemyList, int enemyCount, int raycastCount)
		{
            int currentIndex = 0;
			_lineOfSightRaycastCommands = new NativeArray<RaycastCommand>(raycastCount, Allocator.TempJob);
			_lineOfSightRaycastHits = new NativeArray<RaycastHit>(raycastCount, Allocator.TempJob);

			for (int i = 0; i < enemyCount; i++) {
				var raycastStruct = enemyList[i];
				int raycastPartCount = raycastStruct.Raycasts.Length;

				for (int j = 0; j < raycastPartCount; j++) {
					var raycastPart = raycastStruct.Raycasts[j];
					raycastPart.LineOfSight = false;
					raycastPart.CanShoot = false;

					if (raycastStruct.EnemyDistance > raycastPart.MaxRange) {
						continue;
					}

					Vector3 direction = raycastPart.CastPoint - raycastStruct.EyePosition;
					float distance = direction.magnitude;
					_lineOfSightRaycastCommands[currentIndex + j] = new RaycastCommand(
						raycastStruct.EyePosition, direction, distance, LayerMaskClass.HighPolyWithTerrainMask);
				}

                currentIndex += raycastPartCount;
			}

			_raycastJobHandle = RaycastCommand.ScheduleBatch(_lineOfSightRaycastCommands, _lineOfSightRaycastHits, 2);
		}

        private void setupCanShootJob(IList<EnemyRaycastStruct> enemyList, int enemyCount)
        {
            int currentIndex = 0;
            int raycastCount = _lineOfSightRaycastHits.Length;
            _canShootRaycastCommands = new NativeArray<RaycastCommand>(raycastCount, Allocator.TempJob);
            _canShootRaycastHits = new NativeArray<RaycastHit>(raycastCount, Allocator.TempJob);

            for (int i = 0; i < enemyCount; i++) {
                var raycastStruct = enemyList[i];
                var raycastPartCount = raycastStruct.Raycasts.Length;

                for (int j = 0; j < raycastPartCount; j++) {
                    var raycastPart = raycastStruct.Raycasts[j];
                    var hit = _lineOfSightRaycastHits[currentIndex + j];

                    raycastPart.LineOfSight = hit.collider != null;
                    if (!raycastPart.LineOfSight) {
                        _canShootRaycastCommands[currentIndex + j] = new RaycastCommand(Vector3.zero, Vector3.one, 1f, maxHits: 0);
                        continue;
                    }

                    raycastPart.LOSRaycastHit = hit;
                    Vector3 direction = raycastPart.CastPoint - raycastStruct.WeaponFirePort;
                    float distance = direction.magnitude;
                    
                    _canShootRaycastCommands[currentIndex + j] = new RaycastCommand(
                        raycastStruct.WeaponFirePort, direction, distance, LayerMaskClass.HighPolyWithTerrainMask);
                }

                currentIndex += raycastPartCount;
            }

            _lineOfSightRaycastCommands.Dispose();
            _lineOfSightRaycastHits.Dispose();
            _raycastJobHandle = RaycastCommand.ScheduleBatch(_canShootRaycastCommands, _canShootRaycastHits, 2);
        }
	}
}