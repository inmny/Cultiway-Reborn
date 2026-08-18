using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components
{
    /// <summary>
    /// 玩家在地图上选中地区后出现的底栏，显示旗帜、面积、地区标记、重叠和相邻地区以及内部城市或子地区。
    /// </summary>
    public class SelectedGeoRegionTab : SelectedMeta<GeoRegion, GeoRegionData>
    {
        /// <summary>声明底栏当前展示的是地理区域。</summary>
        public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();
        /// <summary>地区选中底栏本身，供其他入口切换或查询当前页签。</summary>
        public static PowersTab PowersTab {get; private set;}

        // 底栏中依次显示概况、重叠地区、相邻地区和地区内部对象的区域。
        private GeoRegionSelectedTagsContainer _tagsContainer;
        private GeoRegionSelectedRelationsContainer _overlappingRelationsContainer;
        private GeoRegionSelectedRelationsContainer _adjacentRelationsContainer;
        private GeoRegionSelectedMetaContainer _metaContainer;
        // 上次用于排列内容和显示旗帜的地区状态。
        private GeoRegion _layoutRegion;
        private GeoRegion _bannerRegion;
        private int _bannerPresentationRevision;
        // 内容数量变化后置为 true，下一次持续更新时会重新计算底栏大小。
        private bool _layoutDirty = true;

        /// <summary>返回地区选中底栏的登记编号。</summary>
        public override string getPowerTabAssetID()
        {
            return WorldboxGame.PowerTabs.SelectedGeoRegion.id;
        }

        /// <summary>在底栏标题两侧显示通用地区图标和当前地区类别图标。</summary>
        public override void setTitleIcons(GeoRegion pMeta)
        {
            icon_left.sprite = SpriteTextureLoader.getSprite("cultiway/icons/iconGeoRegion");
            icon_right.sprite = pMeta.GetCategory().GetSpriteIcon();
        }

        /// <summary>更新底栏常规数字，并把领土数字显示为地区包含的地块数。</summary>
        public override void showStatsGeneral(GeoRegion pMeta)
        {
            base.showStatsGeneral(pMeta);
            setIconValue("i_territory", pMeta.data.TileCount);
        }

        /// <summary>首次显示或地区外观改变时重画主旗帜，其余时间保持当前画面。</summary>
        public override void checkShowBanner()
        {
            if (banner == null)
            {
                throw new System.InvalidOperationException("GeoRegion 选中底栏缺少主旗帜组件");
            }

            GeoRegion region = nano_object;
            if (ReferenceEquals(_bannerRegion, region) &&
                region != null &&
                _bannerPresentationRevision == region.PresentationRevision)
            {
                return;
            }

            base.checkShowBanner();
            _bannerRegion = region;
            _bannerPresentationRevision = region?.PresentationRevision ?? 0;
        }

        /// <summary>地区没有角色特性，本页不显示原版国家的特性区域。</summary>
        public override void updateTraits()
        {
            // 地区没有角色特性，因此不显示原版国家的特性区域。
        }

        /// <summary>地区或其内容变化时更新底栏数字、概况、关系和内部对象。</summary>
        public override void updateElementsOnChange(GeoRegion pNano)
        {
            bool regionChanged = !ReferenceEquals(_layoutRegion, pNano);
            showStatsGeneral(pNano);
            World.world.selected_buttons.clearHighlightedButton();

            bool containersChanged = _tagsContainer.Refresh(pNano);
            containersChanged |= _overlappingRelationsContainer.Refresh(pNano);
            containersChanged |= _adjacentRelationsContainer.Refresh(pNano);
            containersChanged |= _metaContainer.Refresh(pNano);

            _layoutRegion = pNano;
            _layoutDirty |= regionChanged || containersChanged;
        }

        /// <summary>有内容增减时重新计算底栏尺寸，避免图标被截断或留下大片空白。</summary>
        public override void updateElementsAlways(GeoRegion pNano)
        {
            if (!_layoutDirty) return;
            _layoutDirty = false;
            recalcTabSize();
        }

        /// <summary>注册地区选中底栏，并把原版国家区域改造成地区专用内容。</summary>
        internal static void Init()
        {
            var tab = Manager.CreateSelectedMetaTab<SelectedGeoRegionTab, GeoRegion, GeoRegionData>(WorldboxGame.PowerTabs.SelectedGeoRegion.id);
            tab.SetupGeoRegionMainBanner();
            tab.SetupGeoRegionContainers();

            PowersTab = tab.GetComponent<PowersTab>();
        }

        /// <summary>将原版国家主旗帜替换为地区旗帜，点击后可进入地区详情。</summary>
        private void SetupGeoRegionMainBanner()
        {
            Transform bannerTransform = FindMainBannerTransform();
            bannerTransform.gameObject.SetActive(true);

            var oldBanner = bannerTransform.GetComponent<KingdomBanner>();
            if (oldBanner != null)
            {
                Object.DestroyImmediate(oldBanner);
            }

            var geoRegionBanner = bannerTransform.GetComponent<GeoRegionBanner>() ??
                                  bannerTransform.gameObject.AddComponent<GeoRegionBanner>();
            geoRegionBanner.enable_default_click = true;
            banner = geoRegionBanner;
        }

        /// <summary>寻找底栏主旗帜位置，同时兼容原版节点名和原版国家旗帜组件。</summary>
        private Transform FindMainBannerTransform()
        {
            Transform mainBanner = FindNamedMainBanner();
            if (mainBanner != null)
            {
                return mainBanner;
            }

            KingdomBanner oldBanner = FindMainKingdomBanner();
            if (oldBanner != null)
            {
                return oldBanner.transform;
            }

            throw new System.InvalidOperationException("创建 GeoRegion 选中底栏失败：找不到原版主旗帜节点");
        }

        /// <summary>按名称寻找不属于关系或内部对象区域的主旗帜。</summary>
        private Transform FindNamedMainBanner()
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current.name != "Main Banner") continue;
                if (IsInsideContainer(current)) continue;

                return current;
            }

            return null;
        }

        /// <summary>找不到命名节点时，以原版主国家旗帜的位置作为替换目标。</summary>
        private KingdomBanner FindMainKingdomBanner()
        {
            var oldBanners = GetComponentsInChildren<KingdomBanner>(true);
            for (int i = 0; i < oldBanners.Length; i++)
            {
                KingdomBanner oldBanner = oldBanners[i];
                if (IsInsideContainer(oldBanner.transform)) continue;

                return oldBanner;
            }

            return null;
        }

        /// <summary>判断旗帜是否属于内部对象或关系区域，避免误把列表项当成主旗帜。</summary>
        private static bool IsInsideContainer(Transform child)
        {
            return child.HasAncestorWithAnyComponent(
                typeof(KingdomSelectedMetaBanners),
                typeof(KingdomSelectedAlliesContainer),
                typeof(KingdomSelectedWarsContainer));
        }

        /// <summary>把原版特性、盟友、战争和内部旗帜区域依次替换为地区概况与关系内容。</summary>
        private void SetupGeoRegionContainers()
        {
            _tagsContainer = ReplaceContainer<KingdomSelectedContainerTraits, GeoRegionSelectedTagsContainer>("地区标记区域");

            _overlappingRelationsContainer = ReplaceContainer<KingdomSelectedAlliesContainer, GeoRegionSelectedRelationsContainer>("地区重叠关系区域");
            _overlappingRelationsContainer.Configure(GeoRegionSelectedRelationsContainer.RelationMode.Overlapping);

            _adjacentRelationsContainer = ReplaceContainer<KingdomSelectedWarsContainer, GeoRegionSelectedRelationsContainer>("地区邻接关系区域");
            _adjacentRelationsContainer.Configure(GeoRegionSelectedRelationsContainer.RelationMode.Adjacent);

            _metaContainer = ReplaceContainer<KingdomSelectedMetaBanners, GeoRegionSelectedMetaContainer>("地区子元素区域");
        }

        /// <summary>保留原版区域的位置和标题，换成指定的地区内容组件。</summary>
        private TTarget ReplaceContainer<TSource, TTarget>(string label)
            where TSource : Component
            where TTarget : GeoRegionSelectedContainerBase
        {
            TSource source = GetComponentInChildren<TSource>(true)
                             ?? throw new System.InvalidOperationException($"创建 GeoRegion 选中底栏失败：找不到原版{label}");

            GameObject obj = source.gameObject;
            Transform originalContentRoot = GetOriginalContentRoot(source);
            Object.DestroyImmediate(source);
            obj.SetActive(true);

            TTarget target = obj.GetComponent<TTarget>() ?? obj.AddComponent<TTarget>();
            target.SetOriginalContentRoot(originalContentRoot);
            target.Initialize();
            return target;
        }

        /// <summary>取得原版区域用于摆放图标或旗帜的子节点。</summary>
        private static Transform GetOriginalContentRoot(Component source)
        {
            return source.GetSerializedFieldValue<Transform>("_grid") ??
                   source.GetSerializedFieldValue<Transform>("_container");
        }
    }
}
