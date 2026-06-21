using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using System.Reflection;
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
            // GeoRegion 使用自己的窗口横幅；选中底栏先不复用 Kingdom 的 BannerGeneric 泛型组件。
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
            tab.SetupGeoRegionContainers();

            PowersTab = tab.GetComponent<PowersTab>();
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
            return GetSerializedTransform(source, "_grid") ?? GetSerializedTransform(source, "_container");
        }

        private static Transform GetSerializedTransform(Component source, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            System.Type type = source.GetType();
            while (type != null && type != typeof(MonoBehaviour))
            {
                FieldInfo field = type.GetField(fieldName, flags);
                if (field != null && typeof(Transform).IsAssignableFrom(field.FieldType))
                {
                    return field.GetValue(source) as Transform;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
