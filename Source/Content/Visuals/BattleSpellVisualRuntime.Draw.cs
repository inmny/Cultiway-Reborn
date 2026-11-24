using System.Collections.Generic;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

internal static partial class BattleSpellVisualRuntime
{
    /// <summary>法相随实际持有者移动。</summary>
    /// <param name="cast">真实施放快照。</param><param name="age">世界时间年龄。</param><param name="output">共同模型。</param><param name="details">装饰预算。</param>
    private static void DrawCast(Cast cast,float age,List<SpatialVisualMeshInstance> output,ref int details)
    {
        float opening=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/.24f));
        float radius=Mathf.Max(.3f,cast.Radius);
        if (cast.Style is BattleSpellStyle.Body or BattleSpellStyle.Dragon)
        {
            Vector3 origin = cast.Position;
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
        Vector3 center = cast.Position;
        center.z = .06f;
        DrawDomain(cast, center, radius * opening, age, output, ref details);
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

    /// <summary>领域边界、内部运动与独有结构分层，实际作用半径只由既有领域决定。</summary>
    /// <param name="cast">真实领域。</param><param name="center">中心。</param><param name="radius">真实半径。</param>
    /// <param name="age">年龄。</param><param name="output">共同模型。</param><param name="details">装饰预算。</param>
    private static void DrawDomain(Cast cast,Vector3 center,float radius,float age,List<SpatialVisualMeshInstance> output,ref int details)
    {
        if(radius<.05f)return;
        switch(cast.Style)
        {
            case BattleSpellStyle.Yin:
                CultivationMaterialVisuals.Yin(output,center,radius,age,1f); break;
            case BattleSpellStyle.Yang:
                CultivationMaterialVisuals.Yang(output,center,radius,age,1f); break;
            case BattleSpellStyle.FivePhase:
                CultivationMaterialVisuals.FivePhase(output,center,radius,cast.Phase,cast.PhaseProgress,1f); break;
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
}
