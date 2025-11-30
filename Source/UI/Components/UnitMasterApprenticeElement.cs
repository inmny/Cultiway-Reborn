using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components
{
    public class UnitMasterApprenticeElement : UnitElement
    {
        private const int AVATARS_LIMIT_PER_UNFOLD = 128;

        private const int AVATARS_LIMIT_INITIAL = 16;

        public const float COUNT_ANIMATION_STEP_TIME = 0.025f;

        private const float COUNT_ANIMATION_LENGTH = 0.5f;

        public const float COUNT_ANIMATION_STEPS = 20f;

        public UiUnitAvatarElement prefab_avatar;
        [SerializeField]
        private UnfoldButton _prefab_unfolder;
        private UnfoldButton _grandmaster_unfolder;    // 师祖
        private UnfoldButton _master_unfolder;         // 师父
        private UnfoldButton _siblings_unfolder;       // 同门
        private UnfoldButton _apprentice_unfolder;     // 徒弟
        private ObjectPoolGenericMono<UiUnitAvatarElement> _pool_grandmaster;   // 师祖
        private ObjectPoolGenericMono<UiUnitAvatarElement> _pool_master;        // 师父
        private ObjectPoolGenericMono<UiUnitAvatarElement> _pool_siblings;      // 同门
        private ObjectPoolGenericMono<UiUnitAvatarElement> _pool_apprentice;    // 徒弟

        public Transform transform_grandmaster;    // 师祖
        public Transform transform_master;         // 师父
        public Transform transform_siblings;       // 同门
        public Transform transform_apprentice;     // 徒弟
        public override void Awake()
        {
            if (!_initialized)
            {
                Start();
            }
            base.Awake();
        }
        private bool _initialized = false;
        public override void clear()
        {
            base.clear();
            _pool_grandmaster.clear();
            _pool_master.clear();
            _pool_siblings.clear();
            _pool_apprentice.clear();
            if (_grandmaster_unfolder != null)
            {
                _grandmaster_unfolder.clear();
            }
            if (_master_unfolder != null)
            {
                _master_unfolder.clear();
            }
            if (_siblings_unfolder != null)
            {
                _siblings_unfolder.clear();
            }
            if (_apprentice_unfolder != null)
            {
                _apprentice_unfolder.clear();
            }
        }
        public void Start()
        {
            _initialized = true;
            prefab_avatar = GetComponentInChildren<UiUnitAvatarElement>(true);
            prefab_avatar.gameObject.SetActive(false);

            var tab_title_container_obj = transform.Find("tab_title_container_unit");
            tab_title_container_obj.GetComponentInChildren<LocalizedText>(true).key = "tab_master_apprentice";
            tab_title_container_obj.GetComponentsInChildren<Image>(true).ToList().ForEach(x => {
                if (x.name.ToLower().Contains("icon"))
                {
                    x.sprite = SpriteTextureLoader.getSprite("cultiway/icons/iconMasterTree");
                }
            });

            // 删除所有bg_前缀的children，只保留一个作prefab，然后按师祖、师父、同门、徒弟顺序初始化
            var bg_children = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("bg_"))
                {
                    bg_children.Add(child);
                }
            }

            // 保留第一个bg_做为prefab
            Transform prefab_bg = null;
            if (bg_children.Count > 0)
            {
                prefab_bg = bg_children[0];
                for (int i = 1; i < bg_children.Count; ++i)
                {
                    DestroyImmediate(bg_children[i].gameObject);
                }
            }

            // 初始化四个Transform点和相关对象池/unfolder按钮（bg_xxx顺序按师祖、师父、同门、徒弟）
            if (prefab_bg != null)
            {
                string[] names = new[] { "Grandmasters", "Masters", "Siblings", "Apprentices" };
                Transform[] points = new Transform[4];
                for (int i = 0; i < 4; i++)
                {
                    Transform inst = (i == 0) ? prefab_bg : Instantiate(prefab_bg, prefab_bg.parent);
                    inst.name = "bg_" + names[i].ToLower();
                    inst.gameObject.SetActive(true);
                    inst.GetComponentInChildren<LocalizedText>().key = names[i].ToLower();
                    points[i] = inst.Find("Grid");
                }
                transform_grandmaster = points[0];
                transform_master = points[1];
                transform_siblings = points[2];
                transform_apprentice = points[3];

                // 初始化对象池
                _pool_grandmaster = new ObjectPoolGenericMono<UiUnitAvatarElement>(prefab_avatar, transform_grandmaster);
                _pool_master = new ObjectPoolGenericMono<UiUnitAvatarElement>(prefab_avatar, transform_master);
                _pool_siblings = new ObjectPoolGenericMono<UiUnitAvatarElement>(prefab_avatar, transform_siblings);
                _pool_apprentice = new ObjectPoolGenericMono<UiUnitAvatarElement>(prefab_avatar, transform_apprentice);

                // 初始化unfolder按钮
                if (_prefab_unfolder != null)
                {
                    _grandmaster_unfolder = Instantiate(_prefab_unfolder, transform_grandmaster);
                    _master_unfolder = Instantiate(_prefab_unfolder, transform_master);
                    _siblings_unfolder = Instantiate(_prefab_unfolder, transform_siblings);
                    _apprentice_unfolder = Instantiate(_prefab_unfolder, transform_apprentice);
                }
            }
        }
        public override IEnumerator showContent()
        {
            yield return showGrandmasterContent();
            yield return showMasterContent();
            yield return showSiblingsContent();
            yield return showApprenticeContent();
            yield break;
        }
        private IEnumerator showGrandmasterContent()
        {
            // 无限向上追溯祖师，如多个按顺序全部显示
            if (actor == null || actor.isRekt()) yield break;

            var ae = actor.GetExtend();
            var current = ae;
            var processed = new HashSet<ActorExtend>(); // 防御环引用
            var masters = new List<Actor>();

            // 不包括自己，向上追溯所有师父到最上层
            while (current.HasMaster())
            {
                var master = current.GetMaster();
                if (master == null || master.isRekt())
                    break;
                if (!processed.Add(master.GetExtend()))
                    break; // 防止环引用死循环
                masters.Add(master);
                current = master.GetExtend();
            }

            // 第0个是本角色的师父，再往上均作为祖师显示
            for (int i = 1; i < masters.Count; i++)
            {
                var grandmaster = masters[i];
                var avatar = _pool_grandmaster.getNext();
                avatar.show(grandmaster);
            }

            yield break;
        }
        
        private IEnumerator showMasterContent()
        {
            if (actor == null || actor.isRekt()) yield break;
            
            var ae = actor.GetExtend();
            if (!ae.HasMaster()) yield break;
            
            var master = ae.GetMaster();
            if (master == null || master.isRekt()) yield break;
            
            var avatar = _pool_master.getNext();
            avatar.show(master);
            yield break;
        }
        
        private IEnumerator showSiblingsContent()
        {
            if (actor == null || actor.isRekt()) yield break;
            
            var ae = actor.GetExtend();
            if (!ae.HasMaster()) yield break;
            
            var master = ae.GetMaster();
            if (master == null || master.isRekt()) yield break;
            
            var masterAe = master.GetExtend();
            var apprentices = masterAe.GetApprentices();
            
            // 过滤掉自己，只显示同门
            var siblings = apprentices.Where(app => app.Base != actor && app.Base != null && !app.Base.isRekt()).ToList();
            
            int count = 0;
            int limit = AVATARS_LIMIT_INITIAL;
            foreach (var sibling in siblings)
            {
                if (count >= limit) break;
                var avatar = _pool_siblings.getNext();
                avatar.show(sibling.Base);
                count++;
            }
            
            yield break;
        }
        
        private IEnumerator showApprenticeContent()
        {
            if (actor == null || actor.isRekt()) yield break;
            
            var ae = actor.GetExtend();
            var apprentices = ae.GetApprentices();
            
            int count = 0;
            int limit = AVATARS_LIMIT_INITIAL;
            foreach (var apprentice in apprentices)
            {
                if (apprentice.Base == null || apprentice.Base.isRekt()) continue;
                if (count >= limit) break;
                
                var avatar = _pool_apprentice.getNext();
                avatar.show(apprentice.Base);
                count++;
            }
            
            yield break;
        }
    }
}
