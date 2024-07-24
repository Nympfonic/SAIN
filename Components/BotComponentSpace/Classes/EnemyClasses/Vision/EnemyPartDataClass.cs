using EFT;
using SAIN.Components;
using SAIN.Helpers;
using System.Collections.Generic;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.EnemyClasses
{
    public class EnemyPartDataClass
    {
        public EnemyPartDataClass(EBodyPart bodyPart, BifacialTransform transform, List<BodyPartCollider> colliders)
        {
            BodyPart = bodyPart;
            Transform = transform;
            Colliders = colliders;
            _indexMax = colliders.Count - 1;
            foreach (BodyPartCollider collider in colliders) {
                if (!_colliderDictionary.ContainsKey(collider.BodyPartColliderType)) {
                    _colliderDictionary.Add(collider.BodyPartColliderType, collider);
                }
            }

            //var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //sphere.GetComponent<Renderer>().material.color = Color.red;
            //sphere.GetComponent<Collider>().enabled = false;
            //sphere.name = $"los_line_{_debugCount++}";
            //sphere.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
            //_debugLine = sphere;
            //
            //_debugLineRenderer = sphere.AddComponent<LineRenderer>();
            //// Modify the color and width of the line
            //_debugLineRenderer.material.color = Color.white;
            //_debugLineRenderer.startWidth = 0.02f;
            //_debugLineRenderer.endWidth = 0.01f;
        }

        public void UpdateDebugGizmos(Vector3 eyePosition)
        {
            _debugLineRenderer.SetPosition(0, eyePosition);
            if (!LineOfSight) {
                _debugLineRenderer.material.color = Color.white;

                if (LastRaycastHit != null)
                    _debugLineRenderer.SetPosition(1, LastRaycastHit.Value.point);

                return;
            }

            _debugLineRenderer.material.color = Color.red;
            if (LastSuccessPoint != null) {
                _debugLine.transform.position = LastSuccessPoint.Value;
                _debugLineRenderer.SetPosition(1, LastSuccessPoint.Value);
            }
        }

        private static int _debugCount = 0;
        private GameObject _debugLine;
        private LineRenderer _debugLineRenderer;

        private readonly Dictionary<EBodyPartColliderType, BodyPartCollider> _colliderDictionary = new Dictionary<EBodyPartColliderType, BodyPartCollider>();

        public void SetLineOfSight(BodyPartRaycast result)
        {
            LastRaycastHit = result.LOSRaycastHit;
            float time = Time.time;
            _lastCheckTime = time;
            if (result.LineOfSight) {
                LastSuccessPoint = result.CastPoint;
                _lastVisionSuccessTime = time;
                _lastSuccessPart = _colliderDictionary[result.ColliderType];
            }
            if (result.CanShoot) {
                LastSuccessShootPoint = result.CastPoint;
                _lastShootSucessTime = time;
            }
        }

        public BodyPartRaycast GetRaycast(Vector3 origin, float maxRange)
        {
            BodyPartCollider collider = getCollider();
            Vector3 castPoint = getCastPoint(origin, collider);

            return new BodyPartRaycast {
                LOSRaycastHit = new RaycastHit(),
                ShootRaycastHit = new RaycastHit(),
                VisionRaycastHit = new RaycastHit(),
                CastPoint = castPoint,
                PartType = BodyPart,
                ColliderType = collider.BodyPartColliderType,
                MaxRange = maxRange
            };
        }

        public Vector3? LastSuccessShootPoint { get; private set; }
        public Vector3? LastSuccessPoint { get; private set; }
        public RaycastHit? LastRaycastHit { get; private set; }

        public readonly EBodyPart BodyPart;
        public readonly List<BodyPartCollider> Colliders;
        public readonly BifacialTransform Transform;

        public float TimeSinceLastVisionCheck => Time.time - _lastCheckTime;
        public float TimeSinceLastVisionSuccess => Time.time - _lastVisionSuccessTime;
        public bool LineOfSight => _lastVisionSuccessTime + 0.25f > Time.time;
        public bool CanShoot => _lastShootSucessTime + 0.25f > Time.time;

        private BodyPartCollider getCollider()
        {
            if (_lastSuccessPart != null) {
                return _lastSuccessPart;
            }

            BodyPartCollider collider = Colliders[_index];
            _index++;
            if (_index > _indexMax) {
                _index = 0;
            }
            return collider;
        }

        private int _index;
        private readonly int _indexMax;

        public bool CheckLineOfSight(Vector3 origin, float maxRange)
        {
            if (LineOfSight) {
                return true;
            }

            _lastCheckTime = Time.time;
            BodyPartCollider collider = getCollider();
            Vector3 castPoint = getCastPoint(origin, collider);
            //Vector3 castPoint = _lastSuccessCastPoint ?? getCastPoint(origin, collider);

            Vector3 direction = castPoint - origin;

            float maxRayDistance = direction.magnitude;
            bool lineOfSight = !Physics.Raycast(origin, direction, out var hit, maxRayDistance, LayerMaskClass.HighPolyWithTerrainMask);

            if (lineOfSight) {
                _lastVisionSuccessTime = Time.time;
                _lastSuccessPart = collider;
                LastSuccessPoint = castPoint;
            }
            else {
                _lastSuccessPart = null;
            }

            if (SAINPlugin.DebugMode &&
                _nextdrawTime < Time.time) {
                //_nextdrawTime = Time.time + 0.1f;
                if (lineOfSight) {
                    DebugGizmos.Sphere(castPoint, 0.025f, Color.red, true, 10f);
                    DebugGizmos.Sphere(origin, 0.025f, Color.red, true, 1f);
                    DebugGizmos.Line(castPoint, origin, Color.red, 0.005f, true, 0.5f);
                    //Logger.LogDebug($"{BodyPart} : {maxRayDistance} : {castPoint}");
                }
                else {
                    //DebugGizmos.Sphere(castPoint, 0.025f, Color.white, true, 10f);
                    //DebugGizmos.Sphere(origin, 0.025f, Color.white, true, 1f);
                    //DebugGizmos.Line(castPoint, hit.point, Color.white, 0.005f, true, 0.5f);
                }
            }
            return lineOfSight;
        }

        private const float CHECK_SHOOT_FREQ = 0.1f;
        private const float CHECK_SHOOT_FREQ_AI = 0.2f;

        public bool CheckCanShoot(Vector3 firePort, bool isAI)
        {
            if (!LineOfSight) {
                return false;
            }
            if (LastSuccessPoint == null) {
                return false;
            }
            if (_nextCheckShootTime > Time.time) {
                return CanShoot;
            }
            _nextCheckShootTime = Time.time + (isAI ? CHECK_SHOOT_FREQ_AI : CHECK_SHOOT_FREQ);

            Vector3 point = LastSuccessPoint.Value;
            bool canShoot = canShootToTarget(point, firePort);

            if (!canShoot &&
                !isAI) {
                BodyPartCollider part = getCollider();
                point = getCastPoint(firePort, part);
                canShoot = canShootToTarget(point, firePort);
            }

            if (canShoot) {
                _lastShootSucessTime = Time.time;
                LastSuccessShootPoint = point;
            }
            return canShoot;
        }

        private bool canShootToTarget(Vector3 pointToCheck, Vector3 firePort)
        {
            Vector3 direction = pointToCheck - firePort;
            float distance = direction.magnitude;
            bool canShoot = !Physics.Raycast(firePort, direction, distance, LayerMaskClass.HighPolyWithTerrainMask);
            return canShoot;
        }

        private float _nextCheckShootTime;

        private float _nextdrawTime;

        private float _lastVisionSuccessTime;

        private Vector3 getCastPoint(Vector3 origin, BodyPartCollider collider)
        {
            float size = getColliderMinSize(collider);
            //Logger.LogInfo(size);
            Vector3 random = UnityEngine.Random.insideUnitSphere * size;
            Vector3 result = collider.Collider.ClosestPoint(collider.transform.position + random);
            return result;
        }

        private float getColliderMinSize(BodyPartCollider collider)
        {
            if (collider.Collider == null) {
                return 0f;
            }
            Vector3 bounds = collider.Collider.bounds.size;
            float lowest = bounds.x;
            if (bounds.y < lowest) {
                lowest = bounds.y;
            }
            if (bounds.z < lowest) {
                lowest = bounds.z;
            }
            return lowest;
        }

        private float _lastShootSucessTime;
        private float _lastCheckTime;
        private BodyPartCollider _lastSuccessPart;
    }
}