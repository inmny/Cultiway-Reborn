using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;
using Cultiway.Core;
using NeoModLoader.utils;
using strings;
using UnityEngine;

namespace Cultiway.Content.ActorComponents
{
    public class Train : BaseActorComponent
    {
        class TrainSection : MonoBehaviour
        {
            public int head_index;
            public int tail_index;
            public Vector2 head_pos;
            public Vector2 tail_pos;
            public TrainSectionType type;
            public SpriteAnimation sprite_animation;
            public void UpdatePos(Vector2 head_pos, Vector2 tail_pos)
            {
                this.head_pos = head_pos;
                this.tail_pos = tail_pos;
                // 根据head_pos和tail_pos计算朝向，更新贴图集，并根据夹角微调旋转
                Vector2 dir = head_pos - tail_pos;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // [-180,180]
                Direction direction;
                float spriteAngle;

                // 以45度为界定方向，同时微调旋转角度，正方向为右，顺时针为负
                if (angle >= -45f && angle < 45f)
                {
                    direction = Direction.Right;
                    spriteAngle = angle;
                }
                else if (angle >= 45f && angle < 135f)
                {
                    direction = Direction.Up;
                    spriteAngle = angle - 90f;
                }
                else if (angle >= -135f && angle < -45f)
                {
                    direction = Direction.Down;
                    spriteAngle = angle + 90f;
                }
                else
                {
                    direction = Direction.Left;
                    // Left为180或-180的包区
                    if (angle >= 135f) spriteAngle = angle - 180f;
                    else spriteAngle = angle + 180f;
                }

                // 保证spriteAngle [-45,45]
                if (spriteAngle > 45f) spriteAngle -= 90f;
                if (spriteAngle < -45f) spriteAngle += 90f;

                // 贴图文件说明：左=左图，上=右图
                if (_train_section_sprites.TryGetValue(type, out var dirSprites) && dirSprites.TryGetValue(direction, out var sprites))
                {
                    sprite_animation.setFrames(sprites);
                }
                // 应用微调角度
                sprite_animation.transform.localEulerAngles = new Vector3(0, 0, spriteAngle);


            }
        }
        private List<TrainSection> _train_sections;
        private readonly List<float> _track_segment_lengths = new List<float>();
        private float _track_total_length;
        private float _lead_distance;
        private bool _moving_forward = true;
        private bool _stop_at_end = true;
        private bool _finished_segment;
        private bool _visible = true;
        private const float SectionLength = 4f;
        private void CreateTrainSection(TrainSectionType pType)
        {
            var obj = new GameObject($"TrainSection_{pType}", typeof(SpriteRenderer), typeof(SpriteAnimation), typeof(TrainSection));
            obj.transform.SetParent(transform);
            var section = obj.GetComponent<TrainSection>();
            section.type = pType;
            section.sprite_animation = obj.GetComponent<SpriteAnimation>();
            obj.GetComponent<SpriteRenderer>().sortingLayerName = S_SortingLayer.Objects;
            _train_sections.Add(section);
        }
        public override void create(Actor pActor)
        {
            base.create(pActor);
            _train_sections = new List<TrainSection>();
            CreateTrainSection(TrainSectionType.Head);
            _lead_distance = SectionLength * _train_sections.Count;
            transform.localScale = Vector3.one * 0.3f;
        }
        private List<WorldTile> _track_tiles = new List<WorldTile>();
        public void SetTrackTiles(List<WorldTile> pTrackTiles)
        {
            _finished_segment = false;
            _track_tiles.Clear();
            _track_tiles.AddRange(pTrackTiles);
            RebuildTrackCache();
        }
        public void ConfigureTrack(List<WorldTile> pTrackTiles, bool resetProgress = true, bool stopAtEnd = true)
        {
            SetTrackTiles(pTrackTiles);
            _stop_at_end = stopAtEnd;
            if (resetProgress)
            {
                ResetProgress();
            }
        }
        public void ResetProgress(bool placeAtStart = true)
        {
            _moving_forward = true;
            _finished_segment = false;
            if (_track_total_length <= 0f)
            {
                RebuildTrackCache();
            }

            if (placeAtStart)
            {
                float trainLength = SectionLength * Math.Max(1, _train_sections.Count);
                _lead_distance = Math.Min(trainLength, _track_total_length <= 0f ? trainLength : _track_total_length);
            }
            else
            {
                _lead_distance = _track_total_length;
            }
        }
        public bool IsSegmentFinished => _finished_segment;
        public float TrainLength => SectionLength * (_train_sections?.Count ?? 0);
        public void SetStopAtEnd(bool stop) => _stop_at_end = stop;
        public void Hide() => SetVisibility(false);
        public void Show() => SetVisibility(true);
        private void SetVisibility(bool visible)
        {
            _visible = visible;
            SetSectionsActive(visible);
            if (actor != null)
            {
                gameObject.SetActive(visible);
            }
        }
        private void SetSectionsActive(bool active)
        {
            if (_train_sections == null)
            {
                return;
            }
            foreach (var s in _train_sections)
            {
                if (s == null) continue;
                if (s.gameObject != null)
                {
                    s.gameObject.SetActive(active);
                }
            }
        }
        private void RebuildTrackCache()
        {
            _track_segment_lengths.Clear();
            _track_total_length = 0f;
            if (_track_tiles.Count < 2) return;
            for (int i = 0; i < _track_tiles.Count - 1; i++)
            {
                Vector3 start = _track_tiles[i].posV3;
                Vector3 end = _track_tiles[i + 1].posV3;
                float length = Vector2.Distance(new Vector2(start.x, start.y), new Vector2(end.x, end.y));
                if (length <= 0f) continue;
                _track_segment_lengths.Add(length);
                _track_total_length += length;
            }
            int sectionCount = _train_sections?.Count ?? 0;
            float desiredLead = SectionLength * Math.Max(1, sectionCount);
            if (_track_total_length > 0f)
            {
                _lead_distance = Mathf.Clamp(Math.Max(_lead_distance, desiredLead), 0f, _track_total_length);
            }
        }
        private Vector2 EvaluateTrackPosition(float distance)
        {
            if (_track_tiles.Count == 0)
            {
                return Vector2.zero;
            }
            if (_track_tiles.Count == 1 || _track_total_length <= 0f)
            {
                Vector3 only = _track_tiles[0].posV3;
                return new Vector2(only.x, only.y);
            }
            float remaining = Mathf.Clamp(distance, 0f, _track_total_length);
            for (int i = 0; i < _track_segment_lengths.Count && i + 1 < _track_tiles.Count; i++)
            {
                float segLen = _track_segment_lengths[i];
                if (segLen <= 0f) continue;
                if (remaining <= segLen)
                {
                    Vector3 start = _track_tiles[i].posV3;
                    Vector3 end = _track_tiles[i + 1].posV3;
                    float t = segLen > 0f ? remaining / segLen : 0f;
                    return Vector2.Lerp(new Vector2(start.x, start.y), new Vector2(end.x, end.y), t);
                }
                remaining -= segLen;
            }
            Vector3 last = _track_tiles[_track_tiles.Count - 1].posV3;
            return new Vector2(last.x, last.y);
        }
        public override void update(float pElapsed)
        {
            base.update(pElapsed);
            if (World.world.isPaused() || _train_sections == null || _train_sections.Count == 0 || _track_tiles.Count < 2)
            {
                return;
            }

            if (_track_total_length <= 0f)
            {
                RebuildTrackCache();
                if (_track_total_length <= 0f) return;
            }

            float speed = actor?.stats[S.speed] ?? 0f;
            if (speed < 0f) speed = 0f;
            float trainLength = SectionLength * _train_sections.Count;

            float direction = _moving_forward ? 1f : -1f;
            if (!_finished_segment)
            {
                _lead_distance += direction * speed * pElapsed * 0.01f;
            }
            ModClass.LogInfo($"Train.update: {actor?.data?.id}, {_lead_distance}, {_track_total_length}");

            float minLead = _moving_forward ? Math.Min(trainLength, _track_total_length) : 0f;
            float maxLead = _moving_forward ? _track_total_length : Math.Max(0f, _track_total_length - trainLength);
            if (_track_total_length < trainLength)
            {
                minLead = 0f;
                maxLead = _track_total_length;
            }

            if (_stop_at_end)
            {
                if (_moving_forward && _lead_distance >= maxLead)
                {
                    _lead_distance = maxLead;
                    _finished_segment = true;
                }
                else if (!_moving_forward && _lead_distance <= minLead)
                {
                    _lead_distance = minLead;
                    _finished_segment = true;
                }
                _lead_distance = Mathf.Clamp(_lead_distance, minLead, maxLead);
            }
            else if (_moving_forward && _lead_distance > maxLead)
            {
                float overflow = _lead_distance - maxLead;
                _lead_distance = maxLead - overflow;
                _moving_forward = false;
                direction = -1f;
            }
            else if (!_moving_forward && _lead_distance < minLead)
            {
                float overflow = minLead - _lead_distance;
                _lead_distance = minLead + overflow;
                _moving_forward = true;
                direction = 1f;
            }
            else
            {
                _lead_distance = Mathf.Clamp(_lead_distance, minLead, maxLead);
            }

            for (int i = 0; i < _train_sections.Count; i++)
            {
                float sectionHeadDistance = _lead_distance - direction * i * SectionLength;
                float sectionTailDistance = sectionHeadDistance - direction * SectionLength;
                Vector2 headPos = EvaluateTrackPosition(sectionHeadDistance);
                Vector2 tailPos = EvaluateTrackPosition(sectionTailDistance);
                var section = _train_sections[i];
                section.UpdatePos(headPos, tailPos);
                if (_visible)
                {
                    section.transform.position = headPos;
                    section.sprite_animation?.update(pElapsed);
                }
            }

            if (actor != null)
            {
                actor.current_position = EvaluateTrackPosition(_lead_distance);
            }
        }
        enum Direction
        {
            Left,
            Right,
            Up,
            Down,
        }
        enum TrainSectionType
        {
            Head,
            Cargo,
            Passenger,
        }
        private static Dictionary<TrainSectionType, Dictionary<Direction, Sprite[]>> _train_section_sprites = new Dictionary<TrainSectionType, Dictionary<Direction, Sprite[]>>();
        internal static void Init()
        {
            _train_section_sprites[TrainSectionType.Head] = new Dictionary<Direction, Sprite[]>();
            _train_section_sprites[TrainSectionType.Cargo] = new Dictionary<Direction, Sprite[]>();
            _train_section_sprites[TrainSectionType.Passenger] = new Dictionary<Direction, Sprite[]>();
            _train_section_sprites[TrainSectionType.Head][Direction.Left] = SpriteTextureLoader.getSpriteList("actors/train/head/left");
            _train_section_sprites[TrainSectionType.Head][Direction.Right] = SpriteTextureLoader.getSpriteList("actors/train/head/right");
            _train_section_sprites[TrainSectionType.Head][Direction.Up] = SpriteTextureLoader.getSpriteList("actors/train/head/up");
            _train_section_sprites[TrainSectionType.Head][Direction.Down] = SpriteTextureLoader.getSpriteList("actors/train/head/down");
            _train_section_sprites[TrainSectionType.Cargo][Direction.Left] = SpriteTextureLoader.getSpriteList("actors/train/cargo/left");
            _train_section_sprites[TrainSectionType.Cargo][Direction.Right] = SpriteTextureLoader.getSpriteList("actors/train/cargo/right");
            _train_section_sprites[TrainSectionType.Cargo][Direction.Up] = SpriteTextureLoader.getSpriteList("actors/train/cargo/up");
            _train_section_sprites[TrainSectionType.Cargo][Direction.Down] = SpriteTextureLoader.getSpriteList("actors/train/cargo/down");
            _train_section_sprites[TrainSectionType.Passenger][Direction.Left] = SpriteTextureLoader.getSpriteList("actors/train/passenger/left");
            _train_section_sprites[TrainSectionType.Passenger][Direction.Right] = SpriteTextureLoader.getSpriteList("actors/train/passenger/right");
            _train_section_sprites[TrainSectionType.Passenger][Direction.Up] = SpriteTextureLoader.getSpriteList("actors/train/passenger/up");
            _train_section_sprites[TrainSectionType.Passenger][Direction.Down] = SpriteTextureLoader.getSpriteList("actors/train/passenger/down");
            var obj = ModClass.NewPrefabPreview("Train", typeof(Train));



            ResourcesPatch.PatchResource("actors/p_train", obj);
            ActorExtend.RegisterPossibleChildren<Train>();
        }
    }
}
