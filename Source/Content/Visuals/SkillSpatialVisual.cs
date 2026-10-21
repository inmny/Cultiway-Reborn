using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Visuals;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>读取已有轨迹、范围和阶段，组合清楚的主体、通道、区域与附着效果。</summary>
internal static class SkillSpatialVisual
{
    /// <summary>按真实执行体采样；本方法不推进施法或产生命中。</summary>
    /// <param name="entity">现有执行体。</param>
    /// <param name="skill">实际技能及语义调色板。</param>
    /// <param name="position">未投影的位置。</param>
    /// <param name="lifecycle">原有阶段。</param>
    /// <param name="animation">原有阶段计时。</param>
    /// <param name="meshes">本帧共用模型。</param>
    /// <param name="details">可省略的装饰预算。</param>
    internal static void Append(Entity entity, SkillEntity skill, Vector3 position, SkillAnimationLifecycleState lifecycle,
        AnimData animation, List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        if (lifecycle.Phase == SkillAnimationPhase.RecycleReady || !SkillSpatialVisualCatalog.Forms.TryGetValue(skill.Asset.id, out SkillSpatialForm form)) return;
        if(skill.Asset.id == CoreFormationSkills.SkillAssetIdPrefix + "FivePhase")return;
        float progress = Mathf.Clamp01((animation.frame_idx + animation.frame_timer /
            SkillAnimationLifecycle.ResolveCurrentFrameInterval(entity)) / animation.frames.Length);
        float appearance = lifecycle.Phase == SkillAnimationPhase.Appearance ? Mathf.SmoothStep(.15f, 1f, progress) : 1f;
        float fade = lifecycle.Phase == SkillAnimationPhase.Dissipation ? Mathf.Pow(1f - progress, 2f) : 1f;
        float age = entity.GetComponent<AliveTimer>().value;
        bool explicitNature=skill.SkillContainer.TryGetComponent(out Cultiway.Core.SkillLibV3.Settings.SkillSettings natureSettings) &&
            natureSettings.NatureId!=null;
        if((form is SkillSpatialForm.Spirit or SkillSpatialForm.Fire or SkillSpatialForm.Water or SkillSpatialForm.Wind) && explicitNature)
            form=skill.VfxElement==SkillVfxElements.Fire?SkillSpatialForm.Fire:skill.VfxElement==SkillVfxElements.Water?SkillSpatialForm.Water:
                skill.VfxElement==SkillVfxElements.Ice?SkillSpatialForm.Crystal:skill.VfxElement==SkillVfxElements.Wind?SkillSpatialForm.Wind:
                skill.VfxElement==SkillVfxElements.Lightning?SkillSpatialForm.Needle:skill.VfxElement==SkillVfxElements.Metal?SkillSpatialForm.Needle:
                skill.VfxElement==SkillVfxElements.Earth?SkillSpatialForm.Rock:skill.VfxElement==SkillVfxElements.Wood?SkillSpatialForm.Timber:SkillSpatialForm.Spirit;

        Color primary = skill.ColorPalette.HasColor ? SemanticColorResolver.ToVfxColor(skill.ColorPalette.Primary) : skill.VfxElement.AccentColor;
        primary = Color.Lerp(primary, BattleSpellVisualRuntime.ColorOf(BattleSpellVisualRuntime.Style(skill)), .72f);
        primary.a = fade;
        Color light = Color.Lerp(primary, Color.white, .4f); light.a = fade;
        float radius = Mathf.Max(.12f, SkillEffectRadius.Effect(entity));
        float body = Mathf.Max(.12f, SkillEffectRadius.Collision(entity)) * .45f;
        Vector3 direction = entity.GetComponent<Rotation>().value;
        direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.right;
        Quaternion facing = Quaternion.FromToRotation(Vector3.forward, direction);
        SkillContext context = entity.GetComponent<SkillContext>();
        int variant = skill.SkillContainer.GetComponent<SkillContainer>().AnimationIndex;
        float seed = variant * .77f + (entity.Id % 37) * .11f;
        if (entity.TryGetComponent(out MotionRibbonTrail trail))
        {
            trail.CoreColor = light; trail.GlowColor = primary;
            trail.CoreWidth = body * .18f; trail.GlowWidth = body * .5f;
            trail.Enabled = lifecycle.Phase != SkillAnimationPhase.Dissipation;
            entity.GetComponent<MotionRibbonTrail>() = trail;
        }
        // 蓝图可以改变轨迹。线性结构始终采用执行体实际保存的两端与长度。
        if (entity.TryGetComponent(out LinearTrajectoryState linear) && linear.Initialized &&
            entity.TryGetComponent(out AnimLinearLayout layout))
        {
            if (layout.Mode == AnimLinearLayoutMode.Tile)
                DrawWall(entity, position, direction, layout.WorldLength, radius, appearance, primary, light, meshes);
            else DrawBeam(linear.Start, linear.End, SkillEffectRadius.Collision(entity), age, appearance, primary, light, meshes, ref details);
            return;
        }
        switch (form)
        {
            case SkillSpatialForm.WorldChange: return;
            case SkillSpatialForm.Field:
                DrawField(entity, position, radius, age, appearance, primary, light, meshes, ref details); return;
            case SkillSpatialForm.Tornado:
                DrawTornado(position, radius, age, seed, appearance, primary, meshes, ref details); return;
            case SkillSpatialForm.Shield:
                if (entity.TryGetComponent(out SkillPersistentState shield))
                    DrawShield(entity, context, position, shield.Length * .5f + shield.Width, age, primary, light, meshes);
                else DrawShield(entity, context, position, radius, age, primary, light, meshes);
                return;
            case SkillSpatialForm.Wall:
                if (entity.TryGetComponent(out SkillPersistentState wall))
                    DrawWall(entity, position, direction, wall.Length, radius, appearance, primary, light, meshes);
                return;
            case SkillSpatialForm.Beam:
                if (entity.TryGetComponent(out LinearTrajectoryState beam) && beam.Initialized)
                    DrawBeam(beam.Start, beam.End, SkillEffectRadius.Collision(entity), age, appearance, primary, light, meshes, ref details);
                return;
            case SkillSpatialForm.Chain:
                SpatialVisualDraw.Ring(meshes, position, body * appearance, primary, Quaternion.LookRotation(direction)); return;
            case SkillSpatialForm.Wave:
                if (skill.Asset != SkillEntities.EarthRidge)
                    MaterialSpellVisuals.Wave(meshes, position, direction, radius, age, fade, ref details,
                        Mathf.Clamp01(age / entity.GetComponent<AliveTimeLimit>().value));
                return;
            case SkillSpatialForm.Vine:
                return;
            case SkillSpatialForm.PoisonBloom:
                MaterialSpellVisuals.PoisonBloom(meshes, context, age, fade);
                return;
            case SkillSpatialForm.Rift:
                DrawRift(position, radius, age, appearance, primary, meshes); return;
            case SkillSpatialForm.Support:
                DrawSupport(position, direction, body, age, appearance, primary, light, meshes); return;
            case SkillSpatialForm.Contact:
                SpatialVisualDraw.Ring(meshes, position, body * (1f + age * 2f), primary,
                    Quaternion.FromToRotation(Vector3.forward, direction));
                SpatialVisualDraw.Mesh(meshes, SpatialVisualGeometry.Spark, position, facing,
                    Vector3.one * body * Mathf.Max(.05f, 1f - age * 2f), light, true); return;
            case SkillSpatialForm.Image:
                BaseSimObject source = context.ResolveSpatialSource();
                if (source?.isActor() == true && !source.isRekt())
                {
                    Actor actor = source.a;
                    Sprite sprite = actor.getSpriteToRender();
                    if (sprite != null)
                    {
                        primary.a *= .3f;
                        SpatialVisualDraw.Mesh(meshes, SpatialSpriteMesh.Get(sprite), position,
                            Quaternion.Euler(90f, 0f, 0f), Vector3.one * actor.actor_scale, primary, true);
                    }
                }
                return;
        }
        body *= appearance;
        switch (form)
        {
            case SkillSpatialForm.Rock:
                SpatialVisualDraw.Mesh(meshes, SpatialMaterialGeometry.Rocks[variant % 4], position,
                    Quaternion.Euler(age * 130f + seed, age * 75f, seed * 19f), Vector3.one * body,
                    new Color(1,1,1,fade), false); break;
            case SkillSpatialForm.Crystal:
                SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Crystal, position - direction * body,
                    facing, new Vector3(body, body, body * 3f), light, false,surface:skill.Asset==SkillEntities.StoneCone?SpatialSurface.Stone:SpatialSurface.Ice); break;
            case SkillSpatialForm.Needle:
                SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Crystal, position - direction * body,
                    facing, new Vector3(body * .35f, body * .35f, body * 4f), light, true,
                    surface:skill.VfxElement==SkillVfxElements.Water?SpatialSurface.Water:skill.VfxElement==SkillVfxElements.Metal?SpatialSurface.Metal:SpatialSurface.Energy); break;
            case SkillSpatialForm.Timber:
                Quaternion trunk = skill.Asset == SkillEntities.FallingTimber ? Quaternion.identity : facing;
                SpatialVisualDraw.Mesh(meshes,SpatialMaterialGeometry.Sample(SpatialMaterialGeometry.Roots[variant % 4], 1f),position,
                    trunk * Quaternion.Euler(0,-90,0), new Vector3(body*3f,body*1.7f,body*1.7f),new Color(1,1,1,fade),false);
                break;
            case SkillSpatialForm.Sword:
                SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Sword, position - direction * body * 2f, facing,
                    new Vector3(body * 2f, body * 2f, body * 4.5f), light, false,surface:SpatialSurface.Metal);
                if(explicitNature && skill.VfxElement==SkillVfxElements.Fire)
                    for(int i=0;i<3;i++)SpatialVisualDraw.Mesh(meshes,SpatialEnergyGeometry.Flame,position-direction*body*(1f+i),
                        Quaternion.FromToRotation(Vector3.forward,-direction),new Vector3(body*.65f,body*.65f,body*1.4f),new Color(1,.4f,.08f,fade*.6f),true,additive:false,surface:SpatialSurface.Flame);
                break;
            case SkillSpatialForm.Blade:
                MaterialSpellVisuals.Blade(meshes, skill.Asset, position, direction, body, age, fade, ref details, explicitNature?skill.VfxElement:null); break;
            case SkillSpatialForm.Fire:
                SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Orb, position, facing, Vector3.one * body * .65f, light, true);
                for (int i = 0; i < 3; i++)
                {
                    float phase = age * 14f + seed + i * 2.1f;
                    Vector3 offset = facing * new Vector3(Mathf.Cos(phase), Mathf.Sin(phase), 0f) * body * .4f;
                    SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Flame, position + offset, Quaternion.FromToRotation(Vector3.forward, -direction),
                        new Vector3(body * .9f, body * .9f, body * (1.2f + .5f * Mathf.Sin(phase))), primary, true,additive:false,
                        surface:skill.Asset==SkillEntities.YangBolt?SpatialSurface.Energy:SpatialSurface.Flame);
                }
                break;
            case SkillSpatialForm.Water:
                SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Orb, position, facing, Vector3.one * body * .75f,
                    new Color(.17f,.59f,.69f,fade*.7f),false,surface:SpatialSurface.Water); break;
            case SkillSpatialForm.Wind:
                for (int i = 0; i < 3; i++)
                    SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Stream, position - direction * body * i * .6f,
                        facing * Quaternion.Euler(0f, 0f, age * 240f + i * 100f), Vector3.one * body * (1f - i * .2f), primary, true);
                break;
            default:
                if(skill.VfxElement==SkillVfxElements.Neg || skill.VfxElement==SkillVfxElements.Curse)
                {CultivationMaterialVisuals.Yin(meshes,position,body*1.6f,age,fade);break;}
                if(skill.VfxElement==SkillVfxElements.Entropy)
                {CultivationMaterialVisuals.Chaos(meshes,position,body*1.6f,age,fade);break;}
                SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Orb, position, Quaternion.Euler(age * 30f, seed * 80f, age * 90f), Vector3.one * body * .65f, light, true);
                SpatialVisualDraw.Mesh(meshes, SpatialVisualGeometry.Arc, position, facing * Quaternion.Euler(age * 50f, 0f, age * 130f), Vector3.one * body, primary, true); break;
        }
    }

    /// <summary>用固定两端建立通道，流动光点是装饰，不承担周期伤害。</summary>
    /// <param name="start">实际起点。</param><param name="end">实际终点。</param>
    /// <param name="width">实际身体尺度派生的宽度。</param><param name="age">原有存活时间。</param>
    /// <param name="appearance">出现阶段进度。</param><param name="primary">主体色。</param><param name="light">亮芯色。</param>
    /// <param name="meshes">共用快照。</param><param name="details">装饰预算。</param>
    internal static void DrawBeam(Vector3 start, Vector3 end, float width, float age, float appearance,
        Color primary, Color light, List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        Vector3 delta = end - start;
        if (delta.sqrMagnitude < .0001f) return;
        Quaternion facing = Quaternion.FromToRotation(Vector3.forward, delta);
        var model = SpatialEnergyGeometry.Orb;
        // 通道外缘直接使用真实碰撞半径，亮芯只是其内部，不能把窄亮线当作全部命中范围。
        float crossSection = Mathf.Max(model.Bounds.extents.x, model.Bounds.extents.y);
        Vector3 scale = new(width / crossSection, width / crossSection, delta.magnitude / model.Bounds.size.z);
        Vector3 center = (start + end) * .5f;
        primary.a *= .38f;
        SpatialVisualDraw.Mesh(meshes, model, center, facing, Vector3.Scale(scale, new Vector3(appearance, appearance, 1)), primary, true,
            additive: false, surface: SpatialSurface.Energy);
        SpatialVisualDraw.Mesh(meshes, model, center, facing, Vector3.Scale(scale, new Vector3(.3f, .3f, 1)), light, true);
        for (int i = 0; i < 4 && details > 0; i++, details--)
        {
            float phase = age * 5 + i * Mathf.PI * .5f;
            Vector3 offset = facing * new Vector3(Mathf.Cos(phase), Mathf.Sin(phase), 0) * width * .65f;
            Vector3 point = Vector3.Lerp(start, end, Mathf.Repeat(age * .8f + i * .25f, 1)) + offset;
            SpatialVisualDraw.Mesh(meshes, SpatialVisualGeometry.Leaf, point, facing * Quaternion.Euler(0, 0, phase * Mathf.Rad2Deg),
                new Vector3(width * .3f, width * .18f, width * .7f), primary, true);
        }
    }

    /// <summary>按真实墙长和厚度布置墙段，实心与流动屏障保留不同外观。</summary>
    /// <param name="entity">已有屏障。</param><param name="center">真实中心。</param><param name="direction">真实长轴。</param>
    /// <param name="length">真实长度。</param><param name="radius">显示尺度。</param><param name="appearance">建立进度。</param>
    /// <param name="color">主体色。</param><param name="light">边缘色。</param><param name="meshes">共用快照。</param>
    private static void DrawWall(Entity entity, Vector3 center, Vector3 direction, float length, float radius,
        float appearance, Color color, Color light, List<SpatialVisualMeshInstance> meshes)
    {
        var asset = entity.GetComponent<SkillEntity>().Asset;
        float thickness = entity.GetComponent<SkillPersistentState>().Width;
        center.z = .02f;
        if (asset == SkillEntities.WaterCurtain)
        {
            SpatialVisualDraw.Mesh(meshes, SpatialMaterialGeometry.Curtain, center, MaterialSpellVisuals.Heading(direction),
                new Vector3(length, Mathf.Max(.12f, thickness), Mathf.Max(radius * .85f,length*.12f) * appearance),
                new Color(.8f,.96f,1,color.a*.65f), false);
            float age = entity.GetComponent<AliveTimer>().value;
            for (int i = 0; i < 7; i++)
            {
                Vector3 point = center + direction * length * ((i + .5f) / 7f - .5f);
                float size = length * (.035f + .009f * Mathf.Sin(i * 3.7f + age * 3f));
                SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Orb, point + Vector3.forward * .04f,
                    Quaternion.identity,new Vector3(size,size*.6f,Mathf.Max(.04f,size*.08f)),
                    new Color(.75f,.9f,.92f,color.a*.3f),true,additive:false,surface:SpatialSurface.None);
            }
            return;
        }
        int pieces = Mathf.Clamp(Mathf.CeilToInt(length / .65f), 2, 16);
        bool solid = asset == SkillEntities.EarthWall || asset == SkillEntities.IceWall || asset == SkillEntities.WoodWall;
        Quaternion facing = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        color.a *= solid ? .9f : .13f;
        for (int i = 0; i < pieces; i++)
        {
            Vector3 point = center + direction * length * ((i + .5f) / pieces - .5f);
            float height = Mathf.Max(.45f, radius * .7f) * appearance * (1f + .09f * Mathf.Sin(i * 3.7f));
            if(asset==SkillEntities.WoodWall)
            {
                float age = entity.GetComponent<AliveTimer>().value;
                float growth = Mathf.Clamp01(age * 2.5f - i * .045f);
                SpatialVisualDraw.Mesh(meshes,SpatialMaterialGeometry.Sample(SpatialMaterialGeometry.Roots[i%4],growth),point,
                    facing*Quaternion.Euler(0,-72f,(i%2)*28f-14f),new Vector3(Mathf.Max(height,length*.12f)*1.3f,length/pieces*1.3f,Mathf.Max(.4f,thickness)*1.5f),
                    new Color(1,1,1,color.a),false);
                continue;
            }
            SpatialVisualMesh model=asset==SkillEntities.EarthWall?SpatialMaterialGeometry.Rocks[i%4]:
                asset==SkillEntities.IceWall?SpatialEnergyGeometry.Crystal:asset==SkillEntities.FireWall?SpatialEnergyGeometry.Flame:
                asset==SkillEntities.WaterCurtain?SpatialEnergyGeometry.Wave:asset==SkillEntities.SwordCurtain?SpatialEnergyGeometry.Sword:
                solid?SpatialVisualGeometry.Block:SpatialEnergyGeometry.Stream;
            SpatialVisualDraw.Mesh(meshes, model,
                point, facing, new Vector3(length / pieces * .98f, Mathf.Max(.06f, thickness), height), color, !solid);
        }
    }

    /// <summary>边界标明真实作用范围，内部只保留稀疏流动。</summary>
    /// <param name="entity">真实场域。</param><param name="center">真实中心。</param><param name="radius">真实作用半径。</param>
    /// <param name="age">存活时间。</param><param name="appearance">建立进度。</param><param name="primary">主体色。</param>
    /// <param name="light">符纹色。</param><param name="meshes">共用快照。</param><param name="details">装饰预算。</param>
    private static void DrawField(Entity entity, Vector3 center, float radius, float age, float appearance, Color primary,
        Color light, List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        var fieldElement=entity.GetComponent<SkillEntity>().VfxElement;
        if(entity.GetComponent<SkillEntity>().SkillContainer.TryGetComponent(out Cultiway.Core.SkillLibV3.Settings.SkillSettings fieldSettings) &&
            fieldSettings.NatureId!=null)
        {
            if(fieldElement==SkillVfxElements.Water)
                SpatialVisualDraw.Mesh(meshes, SpatialMaterialGeometry.Pool, new Vector3(center.x, center.y, .04f), Quaternion.identity,
                    new Vector3(radius * appearance, radius * appearance, .08f), new Color(.8f, .94f, .96f, primary.a * .65f), false);
            else if(fieldElement==SkillVfxElements.Fire)
                for(int i=0;i<7;i++)
                {
                    float turn=i*2.39996f;Vector3 point=center+new Vector3(Mathf.Cos(turn),Mathf.Sin(turn),0)*radius*(i%3)*.27f;
                    SpatialVisualDraw.Mesh(meshes,SpatialEnergyGeometry.Flame,point,Quaternion.Euler(0,0,i*73),
                        new Vector3(radius*.2f,radius*.18f,radius*(.35f+.1f*Mathf.Sin(age*5f+i))),new Color(1,.32f,.05f,primary.a*.5f),true,additive:false,surface:SpatialSurface.Flame);
                }
            else if(fieldElement==SkillVfxElements.Pos)CultivationMaterialVisuals.Yang(meshes,center,radius,age,primary.a);
            else if(fieldElement==SkillVfxElements.Wood)MaterialSpellVisuals.RootCluster(meshes,center,radius,Mathf.Clamp01(age/.5f),primary.a);
            else if(fieldElement==SkillVfxElements.Earth)
                for(int i=0;i<4;i++)SpatialVisualDraw.Mesh(meshes,SpatialMaterialGeometry.Rocks[i],center+new Vector3(Mathf.Cos(i*2.4f),Mathf.Sin(i*2.4f),0)*radius*.45f,
                    Quaternion.Euler(0,0,i*71),new Vector3(radius*.25f,radius*.22f,radius*.35f),new Color(1,1,1,primary.a*.7f),false);
            else if(fieldElement==SkillVfxElements.Ice)CultivationMaterialVisuals.Yin(meshes,center,radius,age,primary.a);
            else if(fieldElement!=SkillVfxElements.Neg && fieldElement!=SkillVfxElements.Entropy)
                for(int i=0;i<6;i++)SpatialVisualDraw.Mesh(meshes,SpatialEnergyGeometry.Cloud,center+new Vector3(Mathf.Cos(i*2.4f+age*.2f),Mathf.Sin(i*2.4f+age*.2f),.08f)*radius*.5f,
                    Quaternion.Euler(0,0,i*71),new Vector3(radius*.3f,radius*.25f,radius*.12f),new Color(primary.r,primary.g,primary.b,primary.a*.2f),true,additive:false);
            if(fieldElement!=SkillVfxElements.Neg && fieldElement!=SkillVfxElements.Entropy)return;
        }

        var element=entity.GetComponent<SkillEntity>().VfxElement;
        if(element==SkillVfxElements.Neg || element==SkillVfxElements.Curse)
        {CultivationMaterialVisuals.Yin(meshes,center,radius,age,primary.a);return;}
        if(element==SkillVfxElements.Entropy)
        {CultivationMaterialVisuals.Chaos(meshes,center,radius,age,primary.a);return;}

        center.z = .025f;
        Color boundary = light; boundary.a *= .48f;
        SkillFieldVisualProfile profile = entity.GetComponent<SkillEntity>().Asset.WorldVisualProfile?.Field;
        if (profile != null) { boundary = profile.BoundaryColor; boundary.a *= primary.a * .6f; }
        if (profile != null)
        {
            DrawFieldRing(profile.PrimaryRing, center, radius, age, primary.a, meshes, ref details);
            DrawFieldRing(profile.SecondaryRing, center, radius, age, primary.a, meshes, ref details);
            if (radius >= profile.AccentMinRadius) DrawFieldRing(profile.AccentRing, center, radius, age, primary.a, meshes, ref details);
            return;
        }
        bool cloud = entity.GetComponent<SkillEntity>().Asset == SkillEntities.PoisonMist;
        for (int i = 0; i < 5 && details > 0; i++, details--)
        {
            float angle = i * Mathf.PI * 2f / 5f + age * .12f;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * .72f;
            Color faint = primary; faint.a *= .24f;
            SpatialVisualDraw.Mesh(meshes, cloud ? SpatialEnergyGeometry.Cloud : SpatialVisualGeometry.Leaf, point,
                cloud ? Quaternion.Euler(35f, 0f, angle * Mathf.Rad2Deg) : Quaternion.Euler(0f, 45f, angle * Mathf.Rad2Deg),
                cloud ? new Vector3(radius * .4f, radius * .4f, radius*.2f) : Vector3.one * Mathf.Min(.32f, radius * .12f), faint, true);
        }
    }

    /// <summary>把已有法阵层次转为枝叶、楔形与记号；数量和旋转继续采用该法术自己的配置。</summary>
    /// <param name="ring">可选层次。</param><param name="center">实际场域中心。</param><param name="radius">实际作用范围。</param>
    /// <param name="age">世界时间进度。</param><param name="fade">当前阶段的不透明度。</param>
    /// <param name="meshes">共同快照。</param><param name="details">剩余装饰预算。</param>
    private static void DrawFieldRing(SkillGlyphRingVisualProfile ring, Vector3 center, float radius, float age,
        float fade, List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        if (ring == null) return;
        float size = Mathf.Clamp(radius * ring.SizeRadiusFactor, ring.MinWorldSize, ring.MaxWorldSize);
        Color color = ring.Color; color.a *= fade * .45f;
        SpatialVisualMesh model = ring.Shape switch
        {
            SkillGlyphShape.Leaf or SkillGlyphShape.Branch => SpatialVisualGeometry.Leaf,
            SkillGlyphShape.Wedge => SpatialEnergyGeometry.Crystal,
            _ => SpatialVisualGeometry.Spark,
        };
        for (int i = 0; i < ring.Count && details > 0; i++, details--)
        {
            float angle = (age * ring.RotationSpeed + i * 360f / ring.Count) * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .03f) * radius * ring.RadiusRatio;
            Quaternion rotation = Quaternion.Euler(35f, 0f, angle * Mathf.Rad2Deg);
            SpatialVisualDraw.Mesh(meshes, model, point, rotation, Vector3.one * size, color, true);
            if (ring.Shape == SkillGlyphShape.Branch)
                SpatialVisualDraw.Rod(meshes, point - rotation * Vector3.up * size * .5f,
                    point + rotation * Vector3.up * size * .5f, .018f, color, false);
        }
    }

    /// <summary>用三组错开的弧形成收窄风柱，留出内部可见空间。</summary>
    /// <param name="position">真实中心。</param><param name="radius">范围。</param><param name="age">真实进度。</param>
    /// <param name="seed">固定外形差别。</param><param name="appearance">出现进度。</param><param name="color">元素色。</param>
    /// <param name="meshes">快照。</param><param name="details">装饰预算。</param>
    private static void DrawTornado(Vector3 position, float radius, float age, float seed, float appearance, Color color,
        List<SpatialVisualMeshInstance> meshes, ref int details)
    {
        for (int i = 0; i < 3; i++)
        {
            Color flow=color;flow.a*=.35f;
            SpatialVisualDraw.Mesh(meshes, SpatialEnergyGeometry.Helix, position,
                Quaternion.Euler(0f, 0f, age * 100f+i*120f+seed),
                new Vector3(radius,radius,radius*1.4f)*appearance, flow, true);
        }
    }

    /// <summary>防御只显示真实护持范围，不制造虚假的弹丸拦截。</summary>
    /// <param name="context">真实载体。</param><param name="position">已有位置。</param><param name="radius">护持半径。</param>
    /// <param name="age">进度。</param><param name="primary">主体色。</param><param name="light">边缘色。</param><param name="meshes">快照。</param>
    private static void DrawShield(Entity entity, SkillContext context, Vector3 position, float radius, float age, Color primary, Color light,
        List<SpatialVisualMeshInstance> meshes)
    {
        BaseSimObject source = context.ResolveSpatialSource();
        if (source != null && !source.isRekt()) position = new Vector3(source.current_position.x, source.current_position.y, source.position_height);
        if (entity.GetComponent<SkillEntity>().VfxElement == SkillVfxElements.Pos)
        {
            int first=meshes.Count;
            CultivationMaterialVisuals.Yang(meshes,position,radius,age,primary.a*.6f);
            if(source?.isActor()==true && !source.isRekt())
                for(int i=first;i<meshes.Count;i++)meshes[i]=meshes[i].WithBodyAnchor(source.a);
            return;
        }
        int begin = meshes.Count;
        MaterialSpellVisuals.Shield(meshes, entity.GetComponent<SkillEntity>(), position, radius, age, primary.a);
        if (source?.isActor() == true && !source.isRekt())
            for (int i = begin; i < meshes.Count; i++) meshes[i] = meshes[i].WithBodyAnchor(source.a);
    }

    /// <summary>用立起的裂隙区别于球形爆炸。</summary>
    /// <param name="position">真实落点。</param><param name="radius">范围。</param><param name="age">进度。</param>
    /// <param name="appearance">建立进度。</param><param name="color">裂隙色。</param><param name="meshes">快照。</param>
    private static void DrawRift(Vector3 position, float radius, float age, float appearance, Color color, List<SpatialVisualMeshInstance> meshes)
    {
        CultivationMaterialVisuals.Chaos(meshes,position,radius*appearance,age,color.a);
    }

    /// <summary>辅助流动朝向真实受益者，不复用攻击爆炸。</summary>
    /// <param name="position">真实位置。</param><param name="direction">实际方向。</param><param name="radius">尺度。</param>
    /// <param name="age">进度。</param><param name="appearance">建立进度。</param><param name="primary">主色。</param>
    /// <param name="light">亮色。</param><param name="meshes">快照。</param>
    private static void DrawSupport(Vector3 position, Vector3 direction, float radius, float age, float appearance, Color primary,
        Color light, List<SpatialVisualMeshInstance> meshes)
    {
        Quaternion facing = Quaternion.FromToRotation(Vector3.forward, direction);
        for (int i = 0; i < 3; i++)
            SpatialVisualDraw.Mesh(meshes, SpatialVisualGeometry.Leaf, position - direction * radius * i,
                facing * Quaternion.Euler(0f, 0f, age * 110f + i * 120f), Vector3.one * radius * appearance, i == 0 ? light : primary, true);
    }
}
