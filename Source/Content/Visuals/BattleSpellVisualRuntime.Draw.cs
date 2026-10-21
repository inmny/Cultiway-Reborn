using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Visuals;

internal static partial class BattleSpellVisualRuntime
{
    /// <summary>主体仍跟随真实执行体，较大结构体现同一次作用而非伪造额外飞弹。</summary>
    /// <param name="cast">真实施放快照。</param><param name="age">世界时间年龄。</param><param name="output">共同模型。</param><param name="details">装饰预算。</param>
    private static void DrawCast(Cast cast,float age,List<SpatialVisualMeshInstance> output,ref int details)
    {
        if (cast.Form == SkillSpatialForm.Shield && !cast.AssetId.StartsWith("active.")) return;
        float opening=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/.24f));
        float radius=Mathf.Max(.3f,cast.Radius);
        bool execution=cast.Entity.IsAvailable();
        if(execution && cast.Entity.TryGetComponent(out LinearTrajectoryState beam) && beam.Initialized &&
            cast.Entity.TryGetComponent(out AnimLinearLayout layout) && layout.Mode!=AnimLinearLayoutMode.Tile)
        {
            return;
        }
        if(cast.AssetId.StartsWith("active.") && (cast.Style==BattleSpellStyle.Body || cast.Style==BattleSpellStyle.Dragon))
        {
            Vector3 origin=cast.Source?.isActor()==true && !cast.Source.isRekt()?Cultiway.Content.Combat.SpatialActorPose.Foot(cast.Source.a):cast.Origin;
            float size=Mathf.Min(23f,cast.Growth*3.2f)*opening;
            Color aura=Alpha(cast.Color,.23f);
            if(cast.Style==BattleSpellStyle.Body)
            {
                Mesh(output,SpatialEnergyGeometry.Avatar,origin,Quaternion.Euler(0,0,-12f),new Vector3(size*.65f,size*.4f,size),aura);
                DrawAvatarArm(cast,origin,size,output);
            }
            else
            {
                Mesh(output,SpatialEnergyGeometry.Dragon,origin,Quaternion.Euler(0,0,age*9f),new Vector3(size,size,size),Alpha(cast.Color,.68f));
            }
            return;
        }
        if(cast.Form is SkillSpatialForm.Field or SkillSpatialForm.Tornado or SkillSpatialForm.Shield ||
            cast.Style is BattleSpellStyle.Yin or BattleSpellStyle.Chaos && cast.AssetId.Contains("Domain"))
        {
            Vector3 center=cast.Position;center.z=.06f;
            DrawDomain(cast,center,radius*opening,age,output,ref details);
            return;
        }
        if(cast.AssetId==SkillEntities.LightningStrike.id)return;
        if(cast.Style==BattleSpellStyle.Dragon && cast.Form==SkillSpatialForm.Contact && cast.ImpactAt<0d)
            Mesh(output,SpatialEnergyGeometry.Dragon,cast.Position,Quaternion.Euler(0,0,age*45f),
                new Vector3(radius*.58f,radius*.58f,radius*.55f)*opening,Alpha(cast.Color,.65f));
        if(cast.Form is SkillSpatialForm.Contact or SkillSpatialForm.Support or SkillSpatialForm.WorldChange)return;
        if(cast.Form is SkillSpatialForm.Wave or SkillSpatialForm.Wall or SkillSpatialForm.Rift)return;
        // 只为雷系保留实际电流；其他飞行主体由各自模型负责。
        Vector3 direction=cast.Direction.sqrMagnitude>.01f?cast.Direction.normalized:Vector3.right;
        if(execution && cast.Entity.TryGetComponent(out Rotation rotation))direction=rotation.value.normalized;
        if(cast.Style==BattleSpellStyle.Lightning)
        {
            Bolt(output,cast.Position-direction*Mathf.Min(8f,cast.Growth),cast.Position,cast.Growth*.12f,
                Alpha(cast.Color,.58f),cast.Entity.Id%8);
            return;
        }

    }

    /// <summary>法相伸臂读取真实掌击；普通近战接触后只做短促收势，不新增攻击。</summary>
    /// <param name="cast">真实状态。</param><param name="origin">身体脚下。</param><param name="size">法相高度。</param><param name="output">共享模型。</param>
    private static void DrawAvatarArm(Cast cast,Vector3 origin,float size,List<SpatialVisualMeshInstance> output)
    {
        Quaternion rotation=Quaternion.Euler(0,0,-12f);Vector3 scale=new(size*.65f,size*.4f,size);
        Vector3 shoulder=Point(new Vector3(.285f,0,.65f)),elbow=Point(new Vector3(.4f,-.02f,.48f)),hand=Point(new Vector3(.36f,-.11f,.36f));
        float extension=0f;Vector3 direction=Vector3.right;
        if(cast.Source?.isActor()==true)
        {
            long id=cast.Source.a.id;double now=World.world.getCurWorldTime();
            if(Skills.ConstructedAbilityService.TryGetBodyPose(id,out var pose))
            {Vector3 offset=pose.Hand-pose.Shoulder;offset.z=0;extension=Mathf.Clamp01(offset.magnitude/Mathf.Max(.001f,pose.Height*.5f));direction=offset.sqrMagnitude>.0001f?offset.normalized:Vector3.right;}
            else if(contacts.TryGetValue(id,out double at))
                extension=Mathf.Clamp01(1f-(float)(now-at)/.28f);
        }
        hand=Vector3.Lerp(hand,shoulder+direction*size*.5f,extension);
        elbow=Vector3.Lerp(elbow,Vector3.Lerp(shoulder,hand,.5f)-Vector3.forward*size*.06f,extension);
        Color color=Alpha(cast.Color,.3f);Limb(shoulder,elbow);Limb(elbow,hand);
        Mesh(output,SpatialEnergyGeometry.Palm,hand,Quaternion.FromToRotation(-Vector3.forward,(hand-elbow).normalized),Vector3.one*size*.8f,color);
        Vector3 Point(Vector3 p)=>origin+rotation*Vector3.Scale(p,scale);
        void Limb(Vector3 start,Vector3 end)=>Mesh(output,SpatialEnergyGeometry.Limb,start,Quaternion.FromToRotation(Vector3.forward,end-start),
            new Vector3(size*.075f,size*.075f,Vector3.Distance(start,end)),color);
    }

    /// <summary>来源处短暂成形，留出人物周围的负空间，远景仍可识别施法起点。</summary>
    /// <param name="cast">实际施放。</param><param name="age">年龄。</param><param name="output">共同模型。</param><param name="details">装饰预算。</param>
    private static void DrawGathering(Cast cast,float age,List<SpatialVisualMeshInstance> output,ref int details)
    {
        if (cast.AssetId.StartsWith("Cultiway.KnightTechnique.") || cast.Form == SkillSpatialForm.Shield) return;
        float fade=Mathf.Sin(Mathf.Clamp01(age/.5f)*Mathf.PI),radius=(.7f+cast.Growth*.5f)*(.7f+age);
        Vector3 origin=cast.Origin;origin.z=.08f;
        for(int i=0;i<4 && details>0;i++,details--)
        {
            float a=i*Mathf.PI*.5f+age;
            Vector3 point=origin+new Vector3(Mathf.Cos(a),Mathf.Sin(a),.1f)*radius;
            Mesh(output,SpatialEnergyGeometry.Cloud,Vector3.Lerp(point,origin,age*1.5f),Quaternion.identity,
                new Vector3(radius*.35f,radius*.35f,radius*.22f),Alpha(cast.Color,.12f*fade));
        }
    }

    /// <summary>领域边界、内部运动与独有结构分层，实际作用半径只由既有领域决定。</summary>
    /// <param name="cast">真实领域。</param><param name="center">中心。</param><param name="radius">真实半径。</param>
    /// <param name="age">年龄。</param><param name="output">共同模型。</param><param name="details">装饰预算。</param>
    private static void DrawDomain(Cast cast,Vector3 center,float radius,float age,List<SpatialVisualMeshInstance> output,ref int details)
    {
        if(radius<.05f)return;
        switch(cast.Style)
        {
            case BattleSpellStyle.Wind:
                for(int i=0;i<4;i++)Mesh(output,SpatialEnergyGeometry.Helix,center,Quaternion.Euler(0,0,age*38f+i*90f),
                    new Vector3(radius,radius,radius*1.5f),Alpha(cast.Color,.25f));
                for(int i=0;i<7;i++)
                {
                    float turn=age*.5f+i*2.4f,height=(i+.5f)/7f;
                    Vector3 cloud=center+new Vector3(Mathf.Cos(turn)*radius*(1f-height*.6f),Mathf.Sin(turn)*radius*(1f-height*.6f),height*radius*1.3f);
                    Mesh(output,SpatialEnergyGeometry.Cloud,cloud,Quaternion.identity,new Vector3(radius*.4f,radius*.3f,radius*.25f),Alpha(cast.Color,.13f));
                }
                break;
            case BattleSpellStyle.Water:
            case BattleSpellStyle.Ice:
                for(int i=0;i<3;i++)Mesh(output,SpatialEnergyGeometry.Wave,center+Vector3.forward*(.1f+i*.22f),Quaternion.Euler(0,0,age*14f+i*120f),
                    new Vector3(radius*.85f,radius*.65f,radius*.5f),Alpha(cast.Color,.35f));
                break;
            case BattleSpellStyle.Yin:
                CultivationMaterialVisuals.Yin(output,center,radius,age,1f); break;
            case BattleSpellStyle.Yang:
                CultivationMaterialVisuals.Yang(output,center,radius,age,1f); break;
            case BattleSpellStyle.Chaos:
                CultivationMaterialVisuals.Chaos(output,center,radius,age,1f); break;
            case BattleSpellStyle.FivePhase:
                CultivationMaterialVisuals.FivePhase(output,center,radius,cast.Phase,cast.PhaseProgress,1f); break;
            case BattleSpellStyle.Wood:
                Roots(output,center,radius,Mathf.Clamp01(age/.6f),cast.Color,ref details);
                break;
            case BattleSpellStyle.Poison:
                for(int i=0;i<6 && details>0;i++,details--)
                {
                    float a=i*Mathf.PI/3f+age*.1f;Vector3 p=center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius*.55f;
                    Mesh(output,SpatialEnergyGeometry.Cloud,p+Vector3.forward*radius*.08f,Quaternion.identity,
                        new Vector3(radius*.45f,radius*.35f,radius*.18f),Alpha(cast.Color,.24f));
                    Mesh(output,SpatialEnergyGeometry.Stream,p,Quaternion.Euler(85,0,a*Mathf.Rad2Deg+age*20f),new Vector3(radius*.35f,radius*.35f,radius*.6f),Alpha(cast.Color,.4f));
                }
                break;
            case BattleSpellStyle.Sword:
                Mesh(output,SpatialEnergyGeometry.Sword,center+Vector3.forward*radius*.2f,Quaternion.Euler(12,0,age*12f),
                    new Vector3(radius*.8f,radius*.8f,radius*1.25f),Alpha(cast.Color,.7f),emissive:false);
                break;
            case BattleSpellStyle.Spirit:
                Vector3 soul=center+Vector3.forward*radius*.62f;
                GlowOrb(output,soul,radius*.3f,Alpha(cast.Color,.48f));
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI*.25f+age*.06f;
                    Vector3 petal=center+new Vector3(Mathf.Cos(angle)*radius*.2f,Mathf.Sin(angle)*radius*.2f,radius*.12f);
                    SpatialVisualDraw.Mesh(output,SpatialVisualGeometry.Leaf,petal,Quaternion.Euler(35,0,angle*Mathf.Rad2Deg),
                        Vector3.one*radius*.42f,Alpha(cast.Color,.48f),true,surface:SpatialSurface.Energy);
                }
                break;
            default:
                for(int i=0;i<6 && details>0;i++,details--)
                {
                    float angle=i*Mathf.PI/3f+age*.08f;Vector3 point=center+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius*.74f;
                    Mesh(output,SpatialEnergyGeometry.Cloud,point,Quaternion.Euler(0,0,angle*Mathf.Rad2Deg),
                        new Vector3(radius*.28f,radius*.28f,radius*.16f),Alpha(cast.Color,.18f));
                }
                break;
        }
    }

    /// <summary>实际落点后先出现主冲击，再向真实边界传播，最后收为细屑。</summary>
    /// <param name="cast">已确认落点。</param><param name="age">落点后年龄。</param><param name="output">共同模型。</param><param name="details">装饰预算。</param>
    private static void DrawImpact(Cast cast,float age,List<SpatialVisualMeshInstance> output,ref int details)
    {
        if (cast.AssetId.StartsWith("Cultiway.KnightTechnique.") || cast.Form == SkillSpatialForm.Shield) return;
        const float duration=1.6f;if(age<0f || age>duration)return;
        float progress=age/duration,fade=Mathf.Pow(1f-progress,1.8f),rise=1f-Mathf.Pow(1f-Mathf.Clamp01(age/.35f),3f);
        float radius=cast.Radius;Vector3 center=cast.Impact;center.z=Mathf.Max(.04f,center.z);
        Color color=Alpha(cast.Color,.8f*fade),light=Alpha(Color.Lerp(cast.Color,Color.white,.55f),fade);
        switch(cast.Style)
        {
            case BattleSpellStyle.Fire:
                for(int i=0;i<9;i++)
                {
                    float angle=i*Mathf.PI*2f/9f;
                    Vector3 point=center+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius*.36f*rise;
                    Vector3 size=new(radius*.26f,radius*.26f,radius*(.28f+.14f*Mathf.Sin(i*4.7f))*rise);
                    Mesh(output,SpatialEnergyGeometry.Flame,point,Quaternion.Euler(0,12,angle*Mathf.Rad2Deg),size,Alpha(cast.Color,.3f*fade),additive:false);
                    Mesh(output,SpatialEnergyGeometry.Flame,point,Quaternion.Euler(0,12,angle*Mathf.Rad2Deg),size*.58f,
                        Alpha(new Color(1f,.68f,.12f),.5f*fade));
                }
                Mesh(output,SpatialEnergyGeometry.Flame,center,Quaternion.Euler(0,0,age*31f),
                    new Vector3(radius*.45f,radius*.45f,radius*.5f)*rise,Alpha(cast.Color,.16f*fade),additive:false);
                GlowOrb(output,center+Vector3.forward*radius*.15f,radius*.33f*(1f-progress),Alpha(new Color(1f,.7f,.25f),.55f*fade));
                break;
            case BattleSpellStyle.Yang:
                CultivationMaterialVisuals.Yang(output, center, radius * rise, age, fade);
                break;
            case BattleSpellStyle.Lightning:
                for(int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI*2f/7f;Vector3 end=center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),.04f)*radius*rise;
                    Bolt(output,center,end,radius*.1f,light,i);
                }
                break;
            case BattleSpellStyle.Earth:
                if (cast.AssetId != SkillEntities.GroundSpike.id && cast.AssetId != SkillEntities.EarthRidge.id)
                    MaterialSpellVisuals.DrawEarthRise(output, center, MaterialSpellVisuals.Heading(cast.Direction), radius * .65f,
                        age, Mathf.Max(0f, age - .15f), cast.Entity.Id % 4, ref details);
                break;
            case BattleSpellStyle.Ice:
                for(int i=0;i<9;i++)
                {
                    float a=i*Mathf.PI*2f/9f;Vector3 point=center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius*.62f;
                    Mesh(output,cast.Style==BattleSpellStyle.Earth?SpatialEnergyGeometry.Peak:SpatialEnergyGeometry.Crystal,
                        point,Quaternion.Euler(0,cast.Style==BattleSpellStyle.Ice?i*4f:0f,a*Mathf.Rad2Deg),
                        new Vector3(radius*.12f,radius*.12f,radius*(.3f+.15f*Mathf.Sin(i*3.1f))*rise*(1f-progress*.7f)),color,cast.Style!=BattleSpellStyle.Earth);
                }
                if(cast.Style==BattleSpellStyle.Earth)
                    for(int i=0;i<5;i++)
                    {
                        float a=i*Mathf.PI*.4f;Vector3 dust=center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),.15f)*radius*.65f*rise;
                        Mesh(output,SpatialEnergyGeometry.Cloud,dust,Quaternion.identity,new Vector3(radius*.32f,radius*.26f,radius*.15f),new Color(.54f,.4f,.23f,.24f*fade));
                    }
                break;
            case BattleSpellStyle.Water:
                if (cast.AssetId != SkillEntities.TidalWave.id)
                    MaterialSpellVisuals.Wave(output, center, cast.Direction, radius, .65f + age, fade, ref details);
                break;
            case BattleSpellStyle.Wood:
                if (cast.AssetId == SkillEntities.NatureGrowthField.id)
                    MaterialSpellVisuals.RootCluster(output, center, radius, rise, fade);
                else if (cast.AssetId != SkillEntities.SpiritVine.id)
                    for (int i = 0; i < 5 && details > 0; i++, details--)
                    {
                        float angle = i * 2.39996f;
                        Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius * progress * .8f;
                        point.z += Mathf.Max(0, radius * (progress - progress * progress) * .8f);
                        SpatialVisualDraw.Mesh(output, SpatialMaterialGeometry.Roots[i % 4][31], point,
                            Quaternion.Euler(age * 80, i * 37, i * 72), new Vector3(radius * .2f, radius * .12f, radius * .12f),
                            new Color(1, 1, 1, fade), false);
                    }
                break;
            case BattleSpellStyle.Dragon:
                Mesh(output,SpatialEnergyGeometry.Dragon,center,Quaternion.Euler(0,0,age*45f),
                    new Vector3(radius*.58f,radius*.58f,radius*.55f)*rise,Alpha(cast.Color,.72f*fade));
                break;
            case BattleSpellStyle.Sword:
            case BattleSpellStyle.Body:
                Mesh(output,SpatialEnergyGeometry.Cloud,center,Quaternion.identity,
                    new Vector3(radius*rise,radius*rise,radius*.3f),Alpha(cast.Color,.22f*fade));
                GlowOrb(output,center+Vector3.forward*radius*.1f,radius*.24f*rise,Alpha(cast.Color,.35f*fade));
                break;
            default:
                Mesh(output,SpatialEnergyGeometry.Cloud,center,Quaternion.Euler(0,0,age*15f),
                    new Vector3(radius*rise,radius*rise,radius*.45f),Alpha(cast.Color,.24f*fade));
                break;
        }
        // 小碎屑先受预算限制，主冲击与真实边界保持完整。
        for(int i=0;i<12 && details>0;i++,details--)
        {
            float angle=i*2.39996f;Vector3 velocity=new(Mathf.Cos(angle),Mathf.Sin(angle),0);
            Vector3 point=center+velocity*radius*(.15f+progress*.8f);
            point.z+=Mathf.Max(0f,radius*(progress*1.8f-progress*progress*1.7f))*(.4f+(i%3)*.2f);
            Mesh(output,SpatialVisualGeometry.Spark,point,Quaternion.FromToRotation(Vector3.forward,velocity),
                Vector3.one*radius*.032f*fade,Alpha(cast.Color,.65f*fade));
        }
    }

    /// <summary>粗主干外保留低透明外光，不用全屏闪白代替雷击。</summary>
    /// <param name="output">模型。</param><param name="start">起点。</param><param name="end">终点。</param><param name="spread">分叉展开。</param><param name="color">颜色。</param><param name="seed">固定种子。</param>
    private static void Bolt(List<SpatialVisualMeshInstance> output,Vector3 start,Vector3 end,float spread,Color color,int seed)
    {
        Vector3 delta=end-start;Quaternion rotation=Quaternion.FromToRotation(Vector3.forward,delta);
        Mesh(output,SpatialEnergyGeometry.Bolts[seed%8],start,rotation,new Vector3(spread,spread,delta.magnitude),color);
        Mesh(output,SpatialEnergyGeometry.Bolts[seed%8],start,rotation,new Vector3(spread*2.3f,spread*2.3f,delta.magnitude),Alpha(color,color.a*.12f));
    }

    /// <summary>有分叉、叶冠和生长节奏的木系结构。</summary>
    /// <param name="output">模型。</param><param name="center">中心。</param><param name="radius">真实范围。</param><param name="growth">生长比例。</param><param name="color">颜色。</param><param name="details">预算。</param>
    private static void Roots(List<SpatialVisualMeshInstance> output,Vector3 center,float radius,float growth,Color color,ref int details)
    {
        MaterialSpellVisuals.RootCluster(output, center, radius, growth, color.a);
    }

    /// <summary>太阳冠焰有宽度和团状起伏，避免均匀细线放射。</summary>
    /// <param name="output">模型。</param><param name="center">日核中心。</param><param name="radius">冠焰尺度。</param>
    /// <param name="age">世界秒数。</param><param name="color">流派颜色。</param>
    private static void Corona(List<SpatialVisualMeshInstance> output,Vector3 center,float radius,float age,Color color)
    {
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI*.25f+age*.12f;
            Vector3 direction=new(Mathf.Cos(angle),Mathf.Sin(angle),.15f*Mathf.Sin(i*2.7f));
            Mesh(output,SpatialEnergyGeometry.Flame,center+direction*radius*.65f,
                Quaternion.FromToRotation(Vector3.forward,direction),new Vector3(radius*.5f,radius*.5f,radius*.85f),color,additive:false);
        }
    }

    /// <summary>同一实体的实心亮核与低强度外围分离。</summary>
    /// <param name="output">模型。</param><param name="point">中心。</param><param name="size">主体半径。</param><param name="color">颜色。</param>
    private static void GlowOrb(List<SpatialVisualMeshInstance> output,Vector3 point,float size,Color color)
    {
        Mesh(output,SpatialEnergyGeometry.Orb,point,Quaternion.identity,Vector3.one*size,color);
        Mesh(output,SpatialEnergyGeometry.Orb,point,Quaternion.identity,Vector3.one*size*1.3f,Alpha(color,color.a*.09f));
    }

    /// <summary>保持两种视角共同的网格入口。</summary>
    /// <param name="output">模型。</param><param name="model">几何。</param><param name="position">地图与高度。</param><param name="rotation">姿态。</param><param name="scale">尺寸。</param><param name="color">颜色。</param><param name="emissive">是否自发光。</param>
    /// <param name="additive">是否叠加亮度；外焰保留普通透明颜色。</param>
    private static void Mesh(List<SpatialVisualMeshInstance> output,SpatialVisualMesh model,Vector3 position,Quaternion rotation,Vector3 scale,Color color,bool emissive=true,bool additive=true)=>
        SpatialVisualDraw.Mesh(output,model,position,rotation,scale,color,emissive,additive:additive);

    /// <summary>只修改透明度，不抹去流派色。</summary>
    /// <param name="color">主色。</param><param name="alpha">透明度。</param>
    private static Color Alpha(Color color,float alpha){color.a=alpha;return color;}

    /// <summary>五相各自独立的主色。</summary>
    /// <param name="index">金木水火土的顺序。</param>
    private static Color PhaseColor(int index)=>index switch{0=>new(.7f,.86f,1),1=>new(.22f,.85f,.35f),2=>new(.15f,.6f,1),3=>new(1,.26f,.05f),_=>new(.9f,.66f,.22f)};
}
