using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components
{
    public class SelectedGeoRegionTab : SelectedMeta<GeoRegion, GeoRegionData>
    {
        public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();
        public static PowersTab PowersTab {get; private set;}

        private GeoRegionSelectedTagsContainer _tagsContainer;
        private GeoRegionSelectedRelationsContainer _overlappingRelationsContainer;
        private GeoRegionSelectedRelationsContainer _adjacentRelationsContainer;
        private GeoRegionSelectedMetaContainer _metaContainer;

        public override string getPowerTabAssetID()
        {
            return WorldboxGame.PowerTabs.SelectedGeoRegion.id;
        }

        public override void setTitleIcons(GeoRegion pMeta)
        {
            icon_left.sprite = SpriteTextureLoader.getSprite("cultiway/icons/iconGeoRegion");
            icon_right.sprite = pMeta.GetCategory().GetSpriteIcon();
        }

        public override void showStatsGeneral(GeoRegion pMeta)
        {
            base.showStatsGeneral(pMeta);
            setIconValue("i_territory", pMeta.data.TileCount);
        }

        public override void checkShowBanner()
        {
            if (banner == null)
            {
                throw new System.InvalidOperationException("GeoRegion 选中底栏缺少主旗帜组件");
            }

            base.checkShowBanner();
        }

        public override void updateTraits()
        {
            // GeoRegion 目前没有原版 traits 容器，避免复用 KingdomSelectedContainerTraits。
        }

        public override void updateElementsOnChange(GeoRegion pNano)
        {
            base.updateElementsOnChange(pNano);
            _tagsContainer.Refresh(pNano);
            _overlappingRelationsContainer.Refresh(pNano);
            _adjacentRelationsContainer.Refresh(pNano);
            _metaContainer.Refresh(pNano);
        }

        internal static void Init()
        {
            var tab = Manager.CreateSelectedMetaTab<SelectedGeoRegionTab, GeoRegion, GeoRegionData>(WorldboxGame.PowerTabs.SelectedGeoRegion.id);
            tab.SetupGeoRegionMainBanner();
            tab.SetupGeoRegionContainers();

            PowersTab = tab.GetComponent<PowersTab>();
        }

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

        private static bool IsInsideContainer(Transform child)
        {
            return child.HasAncestorWithAnyComponent(
                typeof(KingdomSelectedMetaBanners),
                typeof(KingdomSelectedAlliesContainer),
                typeof(KingdomSelectedWarsContainer));
        }

        private void SetupGeoRegionContainers()
        {
            _tagsContainer = ReplaceContainer<KingdomSelectedContainerTraits, GeoRegionSelectedTagsContainer>("地区标记区域");

            _overlappingRelationsContainer = ReplaceContainer<KingdomSelectedAlliesContainer, GeoRegionSelectedRelationsContainer>("地区关联关系区域");
            _overlappingRelationsContainer.Configure(GeoRegionSelectedRelationsContainer.RelationMode.Overlapping);

            _adjacentRelationsContainer = ReplaceContainer<KingdomSelectedWarsContainer, GeoRegionSelectedRelationsContainer>("地区邻接关系区域");
            _adjacentRelationsContainer.Configure(GeoRegionSelectedRelationsContainer.RelationMode.Adjacent);

            _metaContainer = ReplaceContainer<KingdomSelectedMetaBanners, GeoRegionSelectedMetaContainer>("地区子元素区域");
        }

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

        private static Transform GetOriginalContentRoot(Component source)
        {
            return source.GetSerializedFieldValue<Transform>("_grid") ??
                   source.GetSerializedFieldValue<Transform>("_container");
        }
    }
}
