using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cultiway.Core.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>外层装备沿原骨架变换参与接触，不以皮肤或整个人物包络代替。</summary>
internal static partial class BodyWorldfallRenderer
{
    /// <summary>原有甲片生成、部位隐藏与穿着遮蔽。</summary>
    private static MethodInfo armourDress, armourHidden, underArmour, hairAndShoes;

    /// <summary>原显示缓存的装备检查时点。</summary>
    private static FieldInfo armourChecked;

    /// <summary>实际持物的外观、摆放和原有弓形生成。</summary>
    private static MethodInfo heldImage,
        heldLook,
        heldModel,
        weaponPlace,
        shootBow,
        addBow,
        addTool,
        modelByName,
        ensurePose;

    /// <summary>独立弓、弦和箭，以及实例自己的根变换。</summary>
    private static FieldInfo bowString, nocked, instancePosition, instanceBasis, lookKind;

    /// <summary>原有武器类别，不引入可选模组的编译引用。</summary>
    private static object bowKind;

    /// <summary>持物沿用人物的拾取、闪烁和深度属性，逐帧不再查找字段。</summary>
    private static FieldInfo[] itemDisplayFields;

    /// <summary>安装时绑定已有装备接口。</summary>
    /// <param name="assembly">实际三维模组。</param><param name="rig">实际人物骨架。</param>
    private static void InstallAttachments(Assembly assembly, Type rig)
    {
        Type armour = assembly.GetType("FirstPerson.Core.ArmorMeshes", true);
        Type people = assembly.GetType("FirstPerson.People", true);
        armourDress = AccessTools.Method(armour, "Dress", new[]
        {
            meshType,
            meshModel.FieldType, typeof(string), typeof(string), typeof(string), meshType.MakeArrayType()
        });
        armourHidden = AccessTools.Method(armour, "HiddenFor");
        underArmour = AccessTools.Method(people, "LookUnderArmour");
        hairAndShoes = AccessTools.Method(people, "HairAndShoes");
        armourChecked = AccessTools.Field(bodyMesh.DeclaringType, "ArmorChecked");
        Type view = assembly.GetType("FirstPerson.Core.ViewMeshes", true),
            archer = assembly.GetType("FirstPerson.Core.Archer", true);
        heldImage = AccessTools.Method(people, "HeldItem");
        heldLook = AccessTools.Method(view, "LookOf");
        heldModel = AccessTools.Method(view, "Item");
        lookKind = AccessTools.Field(heldLook.ReturnType, "Kind");
        bowKind = Enum.Parse(lookKind.FieldType, "Bow");
        weaponPlace = AccessTools.Method(rig, "WeaponPlace");
        shootBow = AccessTools.Method(archer, "Shoot");
        addBow = AccessTools.Method(archer, "Add");
        addTool = AccessTools.Method(people, "AddTool");
        modelByName = AccessTools.Method(assembly.GetType("FirstPerson.Models", true), "Get", new[] { typeof(string) });
        ensurePose = AccessTools.Method(meshType, "EnsurePose");
        instancePosition = AccessTools.Field(meshType, "Position");
        instanceBasis = AccessTools.Field(meshType, "Basis");
        bowString = AccessTools.Field(bodyMesh.DeclaringType, "BowString");
        nocked = AccessTools.Field(bodyMesh.DeclaringType, "Nocked");
        itemDisplayFields = Array.ConvertAll(new[] { "Tag", "Flash", "Multiply", "NoShadow", "SortDepth" },
            name => AccessTools.Field(meshType, name));
    }

    /// <summary>人形身体及其实际外层装备。</summary>
    private sealed partial class SurfaceBinding
    {
        /// <summary>独立生成的原甲片，仅采集几何；显示仍使用原模组自己的甲片实例。</summary>
        private Array armour;

        /// <summary>实际生成的甲片组数，可能按金属合并多个装备槽。</summary>
        private int armourCount;

        /// <summary>实际工具或搬运物名称；它们并不夺取装备槽中的物品。</summary>
        private string tool, load;

        /// <summary>独立持物资料和实例，绝不写入原人物的显示缓存。</summary>
        private object itemFigure, itemLook, itemKind, arrowModel, itemTemplate;

        /// <summary>原接口需要的实际实例列表和最多三份弓部件。</summary>
        private IList itemList;

        /// <summary>原弓实例：弓身、弓弦及箭。</summary>
        private object[] itemMeshes;

        /// <summary>模型与纯姿态中的变换编号对应。</summary>
        private readonly List<HeldSurface> held = new();

        /// <summary>一个实际持物模型；显示使用独立实例，共享其真实不可变几何。</summary>
        private sealed class HeldSurface
        {
            /// <summary>采集和显示各自的原模型实例。</summary>
            internal object Source, Display;

            /// <summary>原模型部件。</summary>
            internal Array Parts;

            /// <summary>共同姿态中的根和首个部件变换。</summary>
            internal int Root, Start;
        }

        /// <summary>读取原模组实际采用的工具或搬运物条件。</summary>
        /// <param name="actor">实际人物。</param>
        private static (string Tool, string Load) HeldTask(Actor actor)
        {
            if (!actor.asset.use_tool_items || actor.has_attack_target) return default;
            if (actor.isCarryingResources()) return (null, ActorBagExtensions.getItemIDToRender(actor.inventory));
            string requested = actor.ai?.task?.force_hand_tool;
            return (string.IsNullOrEmpty(requested) ? null : requested, null);
        }

        /// <summary>持物方式切换时旧接触也随之失效，不能把已经放下的工具留在手中。</summary>
        private string HeldChangedReason => (figureStage.GetValue(figure).ToString() == "Child" ||
                                                   HeldTask(Actor) == (tool, load)
                                                      ? null
                                                      : "HeldChanged");

        /// <summary>读取真正穿戴的装备，沿用原来的遮蔽规则。</summary>
        /// <param name="fitModel">原显示用于适配甲片的基础身体。</param>
        private void PrepareArmour(object fitModel)
        {
            armour = Array.CreateInstance(meshType, 3);
            if (figureStage.GetValue(figure).ToString() == "Child") return;
            string helmet = Equipment[0]?.getAsset()?.id,
                chest = Equipment[1]?.getAsset()?.id,
                boots = Equipment[2]?.getAsset()?.id;
            armourCount = (int)armourDress.Invoke(null, new[] { mesh, fitModel, helmet, chest, boots, armour });
            underArmour.Invoke(null, new[] { mesh, (object)(chest != null), boots != null });
            object memo = Activator.CreateInstance(hairAndShoes.GetParameters()[0].ParameterType.GetElementType());
            hairAndShoes.Invoke(null, new[] { memo, rig, mesh, helmet, boots });
        }

        /// <summary>甲片的索引、父级和枢轴由原生成器复制基础骨架，直接复用同一组身体变换。</summary>
        /// <param name="surfaces">已有身体表面。</param>
        private void AppendArmour(List<BodySurfacePart> surfaces)
        {
            for (int group = 0; group < armourCount; group++)
            {
                object instance = armour.GetValue(group), model = meshModel.GetValue(instance);
                var native = (Array)modelParts.GetValue(model);
                ulong mask = (ulong)hidden.GetValue(instance);
                ulong helmet = (ulong)armourHidden.Invoke(null, new[] { model, (object)true, false, false }),
                    chest = (ulong)armourHidden.Invoke(null, new[] { model, (object)false, true, false });
                for (int i = 0; i < native.Length; i++)
                {
                    if (i < 64 && (mask & (1UL << i)) != 0) continue;
                    object part = native.GetValue(i);
                    var indices = (int[])partTriangles.GetValue(part);
                    if (indices.Length == 0) continue;
                    var positions = (float[])partPositions.GetValue(part);
                    var vertices = new Vector3[positions.Length / 3];
                    for (int point = 0; point < vertices.Length; point++)
                        vertices[point] = new Vector3(positions[point * 3], positions[point * 3 + 1],
                            positions[point * 3 + 2]);
                    string attachment = (helmet & (1UL << i)) == 0 ? "armor.helmet" :
                        (chest & (1UL << i)) == 0 ? "armor.chest" : "armor.boots";
                    AppendSurface(surfaces, i, vertices, indices, attachment);
                }
            }
        }

        /// <summary>从原物品资料建立持物，工作工具优先于普通装备显示。</summary>
        /// <param name="surfaces">已经采集的身体及甲片。</param>
        private void PrepareHeld(List<BodySurfacePart> surfaces)
        {
            if (figureStage.GetValue(figure).ToString() == "Child") return;
            (tool, load) = HeldTask(Actor);
            itemFigure = Activator.CreateInstance(bodyMesh.DeclaringType, true);
            itemList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(meshType));
            itemMeshes = new[]
            {
                Activator.CreateInstance(meshType), Activator.CreateInstance(meshType),
                Activator.CreateInstance(meshType)
            };
            heldWeapon.SetValue(itemFigure, itemMeshes[0]);
            itemTemplate = Activator.CreateInstance(addTool.GetParameters()[11].ParameterType);
            string weaponId = Equipment[3]?.getAsset()?.id;
            if (weaponId != null)
            {
                object sprites = externalSprites.GetValue(externalInstance.GetValue(null));
                itemLook = heldLook.Invoke(null, new[]
                {
                    weaponId,
                    heldImage.Invoke(null, new[] { Actor, sprites })
                });
                itemKind = lookKind.GetValue(itemLook);
                if (!Equals(itemKind, bowKind))
                    meshModel.SetValue(itemMeshes[0], heldModel.Invoke(null, new[] { itemLook, (object)0 }));
                else arrowModel = modelByName.Invoke(null, new object[] { "arrow" });
            }

            float height = Cultiway.Content.Combat.SpatialActorPose.Height(Actor);
            surfacePose.Invoke(rig, new[] { mesh, (object)0f, 0f, 0f, 0f, 0f, 0f, 0f, walker });
            PlaceSurface(Vector3.zero, height);
            UpdateHeld(height);
            int transform = nativeParts.Length;
            foreach (object instance in itemList)
            {
                object model = meshModel.GetValue(instance);
                if (model == null) continue;
                var native = (Array)modelParts.GetValue(model);
                var piece = new HeldSurface
                {
                    Source = instance, Display = Activator.CreateInstance(meshType), Parts = native,
                    Root = transform++, Start = transform
                };
                meshModel.SetValue(piece.Display, model);
                ensurePose.Invoke(piece.Display, null);
                for (int i = 0; i < native.Length; i++)
                {
                    object part = native.GetValue(i);
                    var positions = (float[])partPositions.GetValue(part);
                    var vertices = new Vector3[positions.Length / 3];
                    for (int p = 0; p < vertices.Length; p++)
                        vertices[p] = new Vector3(positions[p * 3], positions[p * 3 + 1], positions[p * 3 + 2]);
                    surfaces.Add(new BodySurfacePart("item", vertices, (int[])partTriangles.GetValue(part), "item",
                        transform++));
                }

                held.Add(piece);
            }
        }

        /// <summary>持物始终从共同身体求握持位置，不从上一帧画面反推。</summary>
        /// <param name="height">共同身体高度。</param>
        private void UpdateHeld(float height)
        {
            if (itemList == null) return;
            itemList.Clear();
            float body = height * HeightScale;
            if (tool != null || load != null)
            {
                object sprites = externalSprites.GetValue(externalInstance.GetValue(null));
                if ((bool)addTool.Invoke(null, new[]
                    {
                        itemList, Actor, itemFigure, rig, mesh, tool, load, (object)0f,
                        body, heading, sprites, itemTemplate
                    })) return;
            }

            if (itemLook != null && Equals(itemKind, bowKind))
            {
                object bow = shootBow.Invoke(null, new[]
                {
                    rig, mesh, (object)0f, 0f,
                    vectorValue(new Vector3(Mathf.Cos(heading), Mathf.Sin(heading), .1f).normalized), body
                });
                addBow.Invoke(null,
                    new[]
                    {
                        itemList, bow, itemLook, arrowModel, itemMeshes[0], itemMeshes[1], itemMeshes[2], (object)0
                    });
            }
            else if (itemLook != null)
            {
                object[] args = { mesh, itemKind, 0f, body, null, null, 1f, 1f };
                if (!(bool)weaponPlace.Invoke(rig, args)) return;
                instancePosition.SetValue(itemMeshes[0], args[4]);
                instanceBasis.SetValue(itemMeshes[0], args[5]);
                itemList.Add(itemMeshes[0]);
            }
        }

        /// <summary>读取持物自己的根和部件，不让整把物品复用前臂的变换。</summary>
        /// <param name="matrices">本轮共同姿态。</param>
        private void CaptureHeld(Matrix4x4[] matrices)
        {
            foreach (HeldSurface piece in held)
            {
                matrices[piece.Root] = WorldMatrix(piece.Source, -1);
                for (int part = 0; part < piece.Parts.Length; part++)
                    matrices[piece.Start + part] = WorldMatrix(piece.Source, part);
            }
        }

        /// <summary>原生持物被共同姿态接管，物品身份和装备归属保持由原系统维护。</summary>
        /// <param name="draws">本轮原生绘制列表。</param><param name="actual">实际人物的原生显示。</param>
        /// <param name="pose">共同完成的身体及持物姿态。</param><param name="template">实际人物绘制属性。</param>
        private void DisplayHeld(IList draws, object actual, BodySurfacePose pose, object template)
        {
            draws.Remove(heldWeapon.GetValue(actual));
            draws.Remove(bowString.GetValue(actual));
            draws.Remove(nocked.GetValue(actual));
            Vector3 shift = swimming ? Vector3.zero : WorldfallVisualBridge.BodyShift(Actor);
            foreach (HeldSurface piece in held)
            {
                Matrix4x4 root = pose.Matrices[piece.Root];
                instancePosition.SetValue(piece.Display, vectorValue(root.MultiplyPoint3x4(Vector3.zero) + shift));
                instanceBasis.SetValue(piece.Display, matrixValue(root));
                palette.SetValue(piece.Display, palette.GetValue(piece.Source));
                foreach (FieldInfo property in itemDisplayFields)
                    property.SetValue(piece.Display, property.GetValue(template));
                var rotationsOut = (Array)rotations.GetValue(piece.Display);
                var offsetsOut = (Array)offsets.GetValue(piece.Display);
                var scalesOut = (float[])scales.GetValue(piece.Display);
                for (int part = 0; part < piece.Parts.Length; part++)
                {
                    object resource = piece.Parts.GetValue(part);
                    int parent = (int)parentPart.GetValue(resource);
                    Matrix4x4 local = (parent < 0 ? root : pose.Matrices[piece.Start + parent]).inverse *
                                      pose.Matrices[piece.Start + part];
                    Vector3 pivot = ReadVector(partPivot.GetValue(resource));
                    rotationsOut.SetValue(matrixValue(local), part);
                    offsetsOut.SetValue(vectorValue(local.MultiplyPoint3x4(pivot) - pivot), part);
                    scalesOut[part] = 1f;
                }

                draws.Add(piece.Display);
            }
        }
    }
}
