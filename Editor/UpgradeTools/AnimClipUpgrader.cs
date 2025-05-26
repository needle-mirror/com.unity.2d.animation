using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.Common;

namespace UnityEditor.U2D.Animation.Upgrading
{
    internal class AnimClipUpgrader : BaseUpgrader
    {
        static class Contents
        {
            public static readonly string ProgressBarTitle = L10n.Tr("Upgrading Animation Clips");
            public static readonly string VerifyingSelection = L10n.Tr("Verifying the selection");
            public static readonly string UpgradingSpriteKeys = L10n.Tr("Upgrading Sprite Keys");
            public static readonly string UpgradingCategoryLabelHash = L10n.Tr("Upgrading Category and Label hashes");
        }

        enum HashType
        {
            Label,
            Category,
            SpriteKey,
            SpriteHash
        }

        class BindingData
        {
            public string BindingPath;
            public System.Type BindingType;
            public List<KeyData> RawKeys;
            public List<ConvertedKeyData> ConvertedKeys;
        }

        class KeyData
        {
            public HashType HashType;
            public float Time;
            public float Value;
        }

        class ConvertedKeyData
        {
            public float Time;
            public float Value;
            public string Category;
            public string Label;
        }

        const string k_LabelHashId = "m_labelHash";
        const string k_CategoryHashId = "m_CategoryHash";
        const string k_SpriteKeyId = "m_SpriteKey";
        const string k_SpriteHashId = "m_SpriteHash";
        const string k_AnimClipTypeId = "t:AnimationClip";

        static bool IsSpriteHashBinding(EditorCurveBinding b) =>
            b.type == typeof(SpriteResolver)
            && !string.IsNullOrEmpty(b.propertyName)
            && b.propertyName == k_SpriteHashId;

        static bool IsSpriteKeyBinding(EditorCurveBinding b) =>
            b.type == typeof(SpriteResolver)
            && !string.IsNullOrEmpty(b.propertyName)
            && b.propertyName == k_SpriteKeyId;

        static bool IsSpriteCategoryBinding(EditorCurveBinding b) =>
            b.type == typeof(SpriteResolver)
            && !string.IsNullOrEmpty(b.propertyName)
            && b.propertyName == k_CategoryHashId;

        static bool IsSpriteLabelBinding(EditorCurveBinding b) =>
            b.type == typeof(SpriteResolver)
            && !string.IsNullOrEmpty(b.propertyName)
            && b.propertyName == k_LabelHashId;

        static SpriteLibUpgrader s_SpriteLibUpgrader = new SpriteLibUpgrader(false, false);

        internal override List<Object> GetUpgradableAssets()
        {
            Object[] assets = AssetDatabase.FindAssets(k_AnimClipTypeId, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Object>)
                .ToArray();

            AnimationClip[] clips = assets
                .Select(x => x as AnimationClip)
                .Where(clip => clip != null)
                .Where(clip =>
                {
                    EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip)
                        .Where(m => IsSpriteKeyBinding(m) || IsSpriteCategoryBinding(m) || IsSpriteLabelBinding(m))
                        .ToArray();
                    return bindings.Length > 0;
                }).ToArray();

            List<Object> assetList = new List<Object>(clips);
            return assetList;
        }

        internal override UpgradeReport UpgradeSelection(List<ObjectIndexPair> objects)
        {
            List<UpgradeEntry> entries = new List<UpgradeEntry>();

            AssetDatabase.StartAssetEditing();

            string msg;
            foreach (ObjectIndexPair obj in objects)
            {
                m_Logger.Add($"Verifying if the asset {obj.Target} is an AnimationClip.");
                EditorUtility.DisplayProgressBar(
                    Contents.ProgressBarTitle,
                    Contents.VerifyingSelection,
                    GetUpgradeProgress(entries, objects));

                if (obj.Target == null)
                {
                    msg = "The upgrade failed. Invalid selection.";
                    m_Logger.Add(msg);
                    m_Logger.AddLineBreak();
                    entries.Add(new UpgradeEntry()
                    {
                        Result = UpgradeResult.Error,
                        Target = obj.Target,
                        Index = obj.Index,
                        Message = msg
                    });
                    continue;
                }

                AnimationClip clip = obj.Target as AnimationClip;
                if (clip == null)
                {
                    msg = $"The upgrade failed. The asset {obj.Target.name} is not an AnimationClip.";
                    m_Logger.Add(msg);
                    m_Logger.AddLineBreak();
                    entries.Add(new UpgradeEntry()
                    {
                        Result = UpgradeResult.Error,
                        Target = obj.Target,
                        Index = obj.Index,
                        Message = msg
                    });
                    continue;
                }

                List<BindingData> extractedData = ExtractDataFromClip(clip);
                ConvertData(ref extractedData);

                bool wasCleanupSuccessful = CleanupData(ref extractedData);
                if (!wasCleanupSuccessful)
                {
                    msg = $"The upgrade of the clip {obj.Target.name} failed. Some keyframes could not be converted in the animation clip.";
                    m_Logger.Add(msg);
                    m_Logger.AddLineBreak();
                    entries.Add(new UpgradeEntry()
                    {
                        Result = UpgradeResult.Error,
                        Target = obj.Target,
                        Index = obj.Index,
                        Message = msg
                    });
                    continue;
                }

                bool isDataValid = ValidateConvertedData(extractedData, obj, entries);
                if (!isDataValid)
                    continue;

                UpdateClipWithConvertedData(clip, extractedData);
                RemoveOldData(clip, extractedData);

                msg = $"Upgrade successful. The clip {obj.Target.name} now uses the latest SpriteResolver data format.";
                m_Logger.Add(msg);
                m_Logger.AddLineBreak();
                entries.Add(new UpgradeEntry()
                {
                    Result = UpgradeResult.Successful,
                    Target = obj.Target,
                    Index = obj.Index,
                    Message = msg
                });
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.StopAssetEditing();

            EditorUtility.ClearProgressBar();

            UpgradeReport report = new UpgradeReport()
            {
                UpgradeEntries = entries,
                Log = m_Logger.GetLog()
            };

            m_Logger.Clear();
            return report;
        }

        bool ValidateConvertedData(List<BindingData> extractedData, ObjectIndexPair upgradingObject, List<UpgradeEntry> entries)
        {
            bool isDataValid = extractedData.All(data => data.ConvertedKeys.Count != 0);
            if (!isDataValid)
            {
                string msg = $"The upgrade of the clip {upgradingObject.Target.name} failed. One or more bindings could not convert its keyframes to the latest data format.";
                m_Logger.Add(msg);
                m_Logger.AddLineBreak();
                entries.Add(new UpgradeEntry()
                {
                    Result = UpgradeResult.Error,
                    Target = upgradingObject.Target,
                    Index = upgradingObject.Index,
                    Message = msg
                });
            }

            return isDataValid;
        }

        List<BindingData> ExtractDataFromClip(AnimationClip clip)
        {
            BindingData[] spriteHashBindings = ExtractBindingsFromClip(clip, HashType.SpriteHash, IsSpriteHashBinding);
            BindingData[] spriteKeyBindings = ExtractBindingsFromClip(clip, HashType.SpriteKey, IsSpriteKeyBinding);
            BindingData[] categoryBindings = ExtractBindingsFromClip(clip, HashType.Category, IsSpriteCategoryBinding);
            BindingData[] labelBindings = ExtractBindingsFromClip(clip, HashType.Label, IsSpriteLabelBinding);

            List<BindingData> bindings = new List<BindingData>();
            bindings.AddRange(spriteHashBindings);
            bindings.AddRange(spriteKeyBindings);
            bindings.AddRange(categoryBindings);
            bindings.AddRange(labelBindings);

            bindings = MergeBindingData(bindings);
            for (int i = 0; i < bindings.Count; ++i)
                SortKeyData(bindings[i]);
            return bindings;
        }

        BindingData[] ExtractBindingsFromClip(AnimationClip clip, HashType hashType, System.Func<EditorCurveBinding, bool> isBindingFunc)
        {
            EditorCurveBinding[] spriteHashBindings = AnimationUtility.GetCurveBindings(clip)
                .Where(isBindingFunc.Invoke)
                .ToArray();

            BindingData[] bindingData = new BindingData[spriteHashBindings.Length];
            for (int i = 0; i < spriteHashBindings.Length; ++i)
                bindingData[i] = ExtractKeyframesFromClip(clip, spriteHashBindings[i], hashType);

            m_Logger.Add($"Extracting {hashType} bindings from clip. Found {bindingData.Length} bindings.");

            return bindingData;
        }

        BindingData ExtractKeyframesFromClip(AnimationClip clip, EditorCurveBinding curveBinding, HashType hashType)
        {
            string bindingPath = curveBinding.path;
            System.Type bindingType = curveBinding.type;

            AnimationCurve curves = AnimationUtility.GetEditorCurve(clip, curveBinding);
            Keyframe[] keys = curves.keys;
            List<KeyData> keyData = new List<KeyData>(keys.Length);
            keyData.AddRange(keys
                .Select(t => new KeyData() { Time = t.time, Value = t.value, HashType = hashType }));

            BindingData data = new BindingData()
            {
                BindingPath = bindingPath,
                BindingType = bindingType,
                RawKeys = keyData
            };

            m_Logger.Add($"Extracting {hashType} keyframes from clip. Found {keyData.Count} keyframes.");

            return data;
        }

        List<BindingData> MergeBindingData(List<BindingData> bindingData)
        {
            List<BindingData> mergedData = new List<BindingData>();
            for (int i = 0; i < bindingData.Count; i++)
            {
                int index = mergedData.FindIndex(x =>
                    x.BindingPath == bindingData[i].BindingPath &&
                    x.BindingType == bindingData[i].BindingType);

                if (index != -1)
                    mergedData[index].RawKeys.AddRange(bindingData[i].RawKeys);
                else
                    mergedData.Add(bindingData[i]);
            }

            m_Logger.Add($"Merging different types keyframes from the same bindings, into the same binding list. We now have {mergedData.Count} binding lists.");

            return mergedData;
        }

        void SortKeyData(BindingData bindingData)
        {
            bindingData.RawKeys.Sort((a, b) => a.Time.CompareTo(b.Time));
            m_Logger.Add($"Order the keyframe data in binding={bindingData.BindingPath} by time.");
        }

        void ConvertData(ref List<BindingData> bindingData)
        {
            for (int i = 0; i < bindingData.Count; ++i)
            {
                bindingData[i].ConvertedKeys = ConvertKeyData(bindingData[i]);
                MergeKeyData(bindingData[i]);
                RepairMissingKeyData(bindingData[i]);
            }
        }

        List<ConvertedKeyData> ConvertKeyData(BindingData bindingData)
        {
            List<KeyData> keyData = bindingData.RawKeys;
            List<ConvertedKeyData> convertedData = new List<ConvertedKeyData>();
            for (int i = 0; i < keyData.Count; ++i)
            {
                switch (keyData[i].HashType)
                {
                    case HashType.SpriteHash:
                        convertedData.Add(ConvertSpriteHash(keyData[i]));
                        break;
                    case HashType.SpriteKey:
                        convertedData.Add(ConvertSpriteKey(keyData[i]));
                        break;
                    case HashType.Category:
                        convertedData.Add(ConvertSpriteCategory(keyData[i]));
                        break;
                    case HashType.Label:
                        convertedData.Add(ConvertSpriteLabel(keyData[i]));
                        break;
                }

                if (convertedData[i].Category == string.Empty && convertedData[i].Label == string.Empty)
                    m_Logger.Add($"Conversion of key={i} of type={keyData[i].HashType} for binding={bindingData.BindingPath} failed to resolve Category and Label values from the Sprite Libraries in the project.");
            }

            m_Logger.Add($"Converting keyframes into uniformed format for binding={bindingData.BindingPath}");
            return convertedData;
        }

        static ConvertedKeyData ConvertSpriteHash(KeyData keyData)
        {
            int spriteHash = InternalEngineBridge.ConvertFloatToInt(keyData.Value);
            SpriteHashToCategoryAndLabelName(spriteHash, out string categoryName, out string labelName);
            ConvertedKeyData convertedData = new ConvertedKeyData()
            {
                Time = keyData.Time,
                Value = keyData.Value,
                Category = categoryName,
                Label = labelName
            };
            return convertedData;
        }

        static ConvertedKeyData ConvertSpriteKey(KeyData keyData)
        {
            int newHash = InternalEngineBridge.ConvertFloatToInt(keyData.Value);
            int spriteHash = SpriteLibraryUtility.Convert32BitTo30BitHash(newHash);

            SpriteHashToCategoryAndLabelName(spriteHash, out string categoryName, out string labelName);
            ConvertedKeyData convertedData = new ConvertedKeyData()
            {
                Time = keyData.Time,
                Value = InternalEngineBridge.ConvertIntToFloat(spriteHash),
                Category = categoryName,
                Label = labelName
            };
            return convertedData;
        }

        static ConvertedKeyData ConvertSpriteCategory(KeyData keyData)
        {
            int newHash = InternalEngineBridge.ConvertFloatToInt(keyData.Value);
            int categoryHash = SpriteLibraryUtility.Convert32BitTo30BitHash(newHash);
            CategoryHashToCategoryName(categoryHash, out string categoryName);

            ConvertedKeyData convertedData = new ConvertedKeyData()
            {
                Time = keyData.Time,
                Value = 0f,
                Category = categoryName,
                Label = string.Empty
            };
            return convertedData;
        }

        static ConvertedKeyData ConvertSpriteLabel(KeyData keyData)
        {
            int newHash = InternalEngineBridge.ConvertFloatToInt(keyData.Value);
            int labelHash = SpriteLibraryUtility.Convert32BitTo30BitHash(newHash);
            LabelHashToLabelName(labelHash, out string labelName);

            ConvertedKeyData convertedData = new ConvertedKeyData()
            {
                Time = keyData.Time,
                Value = 0f,
                Category = string.Empty,
                Label = labelName
            };
            return convertedData;
        }

        static void SpriteHashToCategoryAndLabelName(int spriteHash, out string categoryName, out string labelName)
        {
            SpriteLibraryAsset[] spriteLibraryAssets = s_SpriteLibUpgrader.GetUpgradableAssets()
                .Cast<SpriteLibraryAsset>().ToArray();

            categoryName = string.Empty;
            labelName = string.Empty;

            foreach (SpriteLibraryAsset spriteLib in spriteLibraryAssets)
            {
                foreach (SpriteLibCategory category in spriteLib.categories)
                {
                    foreach (SpriteCategoryEntry label in category.categoryList)
                    {
                        int combinedHash = SpriteLibrary.GetHashForCategoryAndEntry(category.name, label.name);
                        if (combinedHash == spriteHash)
                        {
                            categoryName = category.name;
                            labelName = label.name;
                            return;
                        }
                    }
                }
            }
        }

        static void CategoryHashToCategoryName(int categoryHash, out string categoryName)
        {
            SpriteLibraryAsset[] spriteLibraryAssets = s_SpriteLibUpgrader.GetUpgradableAssets()
                .Cast<SpriteLibraryAsset>().ToArray();

            categoryName = string.Empty;

            foreach (SpriteLibraryAsset spriteLib in spriteLibraryAssets)
            {
                foreach (SpriteLibCategory category in spriteLib.categories)
                {
                    if (category.hash == categoryHash)
                    {
                        categoryName = category.name;
                        return;
                    }
                }
            }
        }

        static void LabelHashToLabelName(int labelHash, out string labelName)
        {
            SpriteLibraryAsset[] spriteLibraryAssets = s_SpriteLibUpgrader.GetUpgradableAssets()
                .Cast<SpriteLibraryAsset>().ToArray();

            labelName = string.Empty;

            foreach (SpriteLibraryAsset spriteLib in spriteLibraryAssets)
            {
                foreach (SpriteLibCategory category in spriteLib.categories)
                {
                    foreach (SpriteCategoryEntry label in category.categoryList)
                    {
                        if (label.hash == labelHash)
                        {
                            labelName = label.name;
                            return;
                        }
                    }
                }
            }
        }

        void MergeKeyData(BindingData bindingData)
        {
            List<ConvertedKeyData> keys = bindingData.ConvertedKeys;
            for (int i = 0; i < keys.Count; ++i)
            {
                string categoryName = keys[i].Category;

                if (categoryName == string.Empty)
                    continue;

                string labelName = keys[i].Label;
                if (labelName != string.Empty)
                    continue;

                for (int m = 0; m < keys.Count; ++m)
                {
                    labelName = keys[m].Label;
                    if (labelName == string.Empty)
                        continue;
                    if (Mathf.Abs(keys[i].Time - keys[m].Time) > Mathf.Epsilon)
                        continue;

                    keys[m].Category = categoryName;
                    keys[i].Label = labelName;

                    m_Logger.Add($"Merged Category={categoryName} and Label={labelName} at time={keys[i].Time} in binding={bindingData.BindingPath}.");
                }
            }
        }

        void RepairMissingKeyData(BindingData bindingData)
        {
            List<ConvertedKeyData> keys = bindingData.ConvertedKeys;
            string categoryName = string.Empty;
            string labelName = string.Empty;
            for (int i = 0; i < keys.Count; ++i)
            {
                if (keys[i].Category != string.Empty)
                    categoryName = keys[i].Category;
                if (keys[i].Label != string.Empty)
                    labelName = keys[i].Label;

                if (keys[i].Value == 0f && categoryName == string.Empty)
                {
                    m_Logger.Add($"Cannot find a category for keyframe at time={keys[i].Time} in binding={bindingData.BindingPath}.");
                    continue;
                }
                if (keys[i].Value == 0f && labelName == string.Empty)
                {
                    m_Logger.Add($"Cannot find a label for keyframe at time={keys[i].Time} in binding={bindingData.BindingPath}.");
                    continue;
                }

                if (keys[i].Value == 0f)
                {
                    int spriteHash = SpriteLibrary.GetHashForCategoryAndEntry(categoryName, labelName);
                    keys[i].Value = InternalEngineBridge.ConvertIntToFloat(spriteHash);

                    if (keys[i].Value != 0f)
                        m_Logger.Add($"Combining categoryName={categoryName} labelName={labelName} into spriteHash={spriteHash}.");
                    else
                        m_Logger.Add($"Could not repair keyframe at time={keys[i].Time} for binding={bindingData.BindingPath}. The Sprite Library Asset might be missing.");
                }
            }
        }

        bool CleanupData(ref List<BindingData> bindingData)
        {
            foreach (BindingData data in bindingData)
            {
                List<ConvertedKeyData> keys = data.ConvertedKeys;

                List<float> keyTimes = new List<float>();
                for (int i = 0; i < keys.Count; ++i)
                {
                    float time = keys[i].Time;
                    if (i == 0 || (time - keyTimes[keyTimes.Count - 1]) > Mathf.Epsilon)
                        keyTimes.Add(time);
                }

                for (int m = keys.Count - 1; m >= 0; --m)
                {
                    if (keys[m].Value == 0f)
                        keys.RemoveAt(m);
                }

                for (int m = keys.Count - 1; m > 0; --m)
                {
                    if (Mathf.Abs(keys[m].Time - keys[m - 1].Time) < Mathf.Epsilon)
                        keys.RemoveAt(m);
                }

                if (keyTimes.Count == keys.Count)
                    m_Logger.Add($"Cleaned up keyframes for binding={data.BindingPath}. It now has {keys.Count} keyframes.");
                else
                {
                    m_Logger.Add($"Expected {keyTimes.Count} keyframes after cleanup for binding={data.BindingPath}, but ended up with {keys.Count}.");
                    return false;
                }
            }

            return true;
        }

        void UpdateClipWithConvertedData(AnimationClip clip, List<BindingData> convertedBindings)
        {
            EditorCurveBinding[] destData = new EditorCurveBinding[convertedBindings.Count];
            for (int i = 0; i < convertedBindings.Count; ++i)
                destData[i] = EditorCurveBinding.DiscreteCurve(convertedBindings[i].BindingPath, convertedBindings[i].BindingType, k_SpriteHashId);

            AnimationCurve[] curves = new AnimationCurve[destData.Length];
            for (int i = 0; i < curves.Length; ++i)
            {
                List<ConvertedKeyData> convertedKeys = convertedBindings[i].ConvertedKeys;
                Keyframe[] keyFrames = new Keyframe[convertedKeys.Count];
                for (int m = 0; m < keyFrames.Length; ++m)
                {
                    keyFrames[m].inTangent = float.PositiveInfinity;
                    keyFrames[m].outTangent = float.PositiveInfinity;
                    keyFrames[m].time = convertedKeys[m].Time;
                    keyFrames[m].value = convertedKeys[m].Value;
                }

                curves[i] = new AnimationCurve(keyFrames);
            }

            AnimationUtility.SetEditorCurves(clip, destData, curves);
            m_Logger.Add($"Injected updated bindings into AnimationClip={clip.name}.");
        }

        void RemoveOldData(AnimationClip clip, List<BindingData> bindingData)
        {
            EditorCurveBinding[] spriteKeyCurves = new EditorCurveBinding[bindingData.Count];
            EditorCurveBinding[] spriteCategoryCurves = new EditorCurveBinding[bindingData.Count];
            EditorCurveBinding[] spriteLabelCurves = new EditorCurveBinding[bindingData.Count];

            for (int i = 0; i < bindingData.Count; ++i)
            {
                spriteKeyCurves[i] = EditorCurveBinding.DiscreteCurve(bindingData[i].BindingPath, bindingData[i].BindingType, k_SpriteKeyId);
                spriteCategoryCurves[i] = EditorCurveBinding.DiscreteCurve(bindingData[i].BindingPath, bindingData[i].BindingType, k_CategoryHashId);
                spriteLabelCurves[i] = EditorCurveBinding.DiscreteCurve(bindingData[i].BindingPath, bindingData[i].BindingType, k_LabelHashId);
            }

            AnimationUtility.SetEditorCurves(clip, spriteKeyCurves, new AnimationCurve[spriteKeyCurves.Length]);
            AnimationUtility.SetEditorCurves(clip, spriteCategoryCurves, new AnimationCurve[spriteCategoryCurves.Length]);
            AnimationUtility.SetEditorCurves(clip, spriteLabelCurves, new AnimationCurve[spriteLabelCurves.Length]);

            m_Logger.Add($"Removed old bindings in AnimationClip={clip.name}.");
        }

        static float GetUpgradeProgress(List<UpgradeEntry> reports, List<ObjectIndexPair> totalNoOfObjects)
        {
            return reports.Count / (float)totalNoOfObjects.Count;
        }
    }
}
