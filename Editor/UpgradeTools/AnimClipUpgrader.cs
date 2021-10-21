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
        
        class CategoryLabelSet
        {
            public float Time;
            public float Category;
            public float Label;

            public CategoryLabelSet(float time, float category, float label)
            {
                Time = time;
                Category = category;
                Label = label;
            }
        }        
        
        const string k_LabelHashId = "m_labelHash";
        const string k_CategoryHashId = "m_CategoryHash";
        const string k_SpriteKeyId = "m_SpriteKey"; 
        const string k_SpriteHashId = "m_SpriteHash";
        const string k_AnimClipTypeId = "t:AnimationClip";

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
            var assets = AssetDatabase.FindAssets(k_AnimClipTypeId, new [] {"Assets"})
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Object>)
                .ToArray();

            var clips = assets
                .Select(x => x as AnimationClip)
                .Where(clip => clip != null)
                .Where(clip =>
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip)
                        .Where(m => IsSpriteKeyBinding(m) || IsSpriteCategoryBinding(m) || IsSpriteLabelBinding(m))
                        .ToArray();
                    return bindings.Length > 0;
                }).ToArray();

            var assetList = new List<Object>(clips);
            return assetList;
        }      
        
        internal override UpgradeReport UpgradeSelection(List<ObjectIndexPair> objects)
        {
            var entries = new List<UpgradeEntry>();

            AssetDatabase.StartAssetEditing();

            string msg;
            foreach (var obj in objects)
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
                
                var clip = obj.Target as AnimationClip;
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

                var sourceBindings = AnimationUtility.GetCurveBindings(clip)
                    .Where(IsSpriteKeyBinding)
                    .ToArray();
                if (sourceBindings.Length > 0)
                {
                    m_Logger.Add($"The clip {clip.name} contains Sprite Keys. Starting the upgrade.");
                    EditorUtility.DisplayProgressBar(
                        Contents.ProgressBarTitle, 
                        Contents.UpgradingSpriteKeys, 
                        GetUpgradeProgress(entries, objects));
                    
                    var successfulUpgrade = UpgradeFromSpriteKey(clip, sourceBindings);
                    if (!successfulUpgrade)
                    {
                        msg = $"The upgrade of the clip {obj.Target.name} failed. Could not convert the Sprite Key values into new Sprite Hash values.";
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
                }
                else
                {
                    m_Logger.Add($"The clip {clip.name} does not contain Sprite Keys. Looking for Category and Label hash.");
                    EditorUtility.DisplayProgressBar(
                        Contents.ProgressBarTitle, 
                        Contents.UpgradingCategoryLabelHash, 
                        GetUpgradeProgress(entries, objects));
                    
                    var categoryBindings = AnimationUtility.GetCurveBindings(clip)
                        .Where(IsSpriteCategoryBinding)
                        .ToArray();
                    var labelBindings = AnimationUtility.GetCurveBindings(clip)
                        .Where(IsSpriteLabelBinding)
                        .ToArray();
                    if (categoryBindings.Length == 0 && labelBindings.Length == 0)
                    {
                        msg = $"The upgrade of the clip {clip.name} was cancelled. The Animation Clip does not have any data in need of upgrading.";
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
                    if (categoryBindings.Length == 0 || labelBindings.Length == 0)
                    {
                        msg = $"The upgrade of the clip {clip.name} failed. Both Category hash and Label hash are required in the same clip for a successful upgrade.";
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

                    var isValid = true;
                    foreach (var cat in categoryBindings)
                    {
                        var hasSamePath = false;
                        foreach (var label in labelBindings)
                        {
                            if (cat.path == label.path)
                            {
                                hasSamePath = true;
                                break;
                            }
                        }

                        if (!hasSamePath)
                        {
                            isValid = false;
                            break;
                        }
                    }
                    
                    if (!isValid)
                    {
                        msg = $"The upgrade of the clip {obj.Target.name} failed. Both Category hash and Label hash is required per SpriteResolver for a successful upgrade.";
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

                    var successfulUpgrade = UpgradeFromCategoryAndLabel(clip, categoryBindings, labelBindings);
                    if (!successfulUpgrade)
                    {
                        msg = $"The upgrade of the clip {obj.Target.name} failed. Could not convert the Category and Label hash into one unified hash.";
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
                }

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
            
            var report = new UpgradeReport()
            {
                UpgradeEntries = entries,
                Log = m_Logger.GetLog()
            };
            
            m_Logger.Clear();
            return report;
        }

        static float GetUpgradeProgress(List<UpgradeEntry> reports, List<ObjectIndexPair> totalNoOfObjects)
        {
            return reports.Count / (float)totalNoOfObjects.Count;
        }
        
        bool UpgradeFromSpriteKey(AnimationClip clip, EditorCurveBinding[] sourceBindings)
        {
            var destBindings = new EditorCurveBinding[sourceBindings.Length];
            for (var i = 0; i < sourceBindings.Length; ++i)
                destBindings[i] = EditorCurveBinding.DiscreteCurve(sourceBindings[i].path, sourceBindings[i].type, k_SpriteHashId);

            var curves = new AnimationCurve[sourceBindings.Length];
            for (var i = 0; i < sourceBindings.Length; ++i)
            {
                curves[i] = AnimationUtility.GetEditorCurve(clip, sourceBindings[i]);
                var keys = curves[i].keys;
                for (var m = 0; m < keys.Length; ++m)
                {
                    var newHash = InternalEngineBridge.ConvertFloatToInt(keys[m].value);
                    newHash = SpriteLibraryUtility.Convert32BitTo30BitHash(newHash);
                    if (newHash == 0)
                    {
                        m_Logger.Add($"Upgrading the clip {clip.name} Sprite Keys resulted in a zero value hash. This is an invalid hash. Aborting the upgrade.");
                        return false;
                    }

                    keys[m].value = InternalEngineBridge.ConvertIntToFloat(newHash); 
                }
                curves[i].keys = keys;
            }
            
            AnimationUtility.SetEditorCurves(clip, sourceBindings, new AnimationCurve[sourceBindings.Length]);
            AnimationUtility.SetEditorCurves(clip, destBindings, curves);
            return true;
        }

        bool UpgradeFromCategoryAndLabel(AnimationClip clip, EditorCurveBinding[] categoryBindings, EditorCurveBinding[] labelBindings)
        {
            var spriteLibs = s_SpriteLibUpgrader.GetUpgradableAssets()
                .Cast<SpriteLibraryAsset>().ToArray();
            
            var destBindings = new EditorCurveBinding[categoryBindings.Length];
            for (var i = 0; i < categoryBindings.Length; ++i)
                destBindings[i] = EditorCurveBinding.DiscreteCurve(categoryBindings[i].path, categoryBindings[i].type, k_SpriteHashId);

            var curves = new AnimationCurve[destBindings.Length];
            for (var i = 0; i < destBindings.Length; ++i)
            {
                var catCurve = AnimationUtility.GetEditorCurve(clip, categoryBindings[i]);
                var labelCurve = AnimationUtility.GetEditorCurve(clip, labelBindings[i]);

                var catKeys = catCurve.keys;
                var labelKeys = labelCurve.keys;
                m_Logger.Add($"Found {catKeys.Length} Category keys and {labelKeys.Length} Label keys.");
                
                var sets = catKeys
                    .Select(catKey => new CategoryLabelSet(catKey.time, catKey.value, 0f)).ToList();
                sets.AddRange(labelKeys
                    .Select(labelKey => new CategoryLabelSet(labelKey.time, 0f, labelKey.value)));

                sets.Sort((x, y) => x.Time.CompareTo(y.Time));

                var currentCat = 0f;
                var currentLabel = 0f;
                foreach (var set in sets)
                {
                    if (set.Category != 0f)
                        currentCat = set.Category;
                    if (set.Label != 0f)
                        currentLabel = set.Label;
                    set.Category = currentCat;
                    set.Label = currentLabel;
                }
                sets = sets.Where(x => x.Category != 0f && x.Label != 0f).ToList();

                for (var m = sets.Count - 1; m > 0; --m)
                {
                    if (Mathf.Abs(sets[m].Time - sets[m - 1].Time) < Mathf.Epsilon)
                        sets.RemoveAt(m);
                }

                if (sets.Count == 0)
                {
                    m_Logger.Add("Could not create any sets of Category and Label hash where both hashes were non zero. Aborting the upgrade.");
                    return false;   
                }
                m_Logger.Add($"Created {sets.Count} sets of Category and Label keys.");

                var newKeys = new Keyframe[sets.Count];
                for (var m = 0; m < newKeys.Length; ++m)
                {
                    var newHash = ConvertCategoryAndLabel(spriteLibs, sets[m].Category, sets[m].Label);
                    if (newHash == 0)
                    {
                        m_Logger.Add($"Failed to resolve a Category and Label set. This would result in loss of data if the upgrade proceeds. Aborting the upgrade.");
                        return false;   
                    }

                    newKeys[m] = catKeys[0];
                    newKeys[m].time = sets[m].Time;
                    newKeys[m].value = InternalEngineBridge.ConvertIntToFloat(newHash);
                }

                curves[i] = catCurve;
                curves[i].keys = newKeys;
                m_Logger.Add($"Created {newKeys.Length} new keys.");
            }
            
            AnimationUtility.SetEditorCurves(clip, categoryBindings, new AnimationCurve[categoryBindings.Length]);
            AnimationUtility.SetEditorCurves(clip, labelBindings, new AnimationCurve[labelBindings.Length]);
            AnimationUtility.SetEditorCurves(clip, destBindings, curves);
            return true;
        }

        int ConvertCategoryAndLabel(SpriteLibraryAsset[] spriteLibraryAssets, float categoryHash, float labelHash)
        {
            var convertedCat = SpriteLibraryUtility.Convert32BitTo30BitHash(InternalEngineBridge.ConvertFloatToInt(categoryHash));
            var convertedLabel = SpriteLibraryUtility.Convert32BitTo30BitHash(InternalEngineBridge.ConvertFloatToInt(labelHash));

            var categoryString = string.Empty;
            var labelString = string.Empty;
            foreach (var spriteLib in spriteLibraryAssets)
            {
                foreach (var category in spriteLib.categories)
                {
                    if (category.hash == convertedCat)
                    {
                        categoryString = category.name;
                        foreach (var label in category.categoryList)
                        {
                            if (label.hash == convertedLabel)
                            {
                                labelString = label.name;
                                break;
                            }
                        }
                        
                        if(labelString != string.Empty)
                            break;
                    }
                }
            }

            if (string.IsNullOrEmpty(categoryString))
            {
                m_Logger.Add($"Could not pair the Category hash {categoryHash} with a Category in any Sprite Libraries in the project. Aborting the upgrade.");
                return 0;   
            }
            if (string.IsNullOrEmpty(labelString))
            {
                m_Logger.Add($"Could not pair the Label hash {labelHash} with a Label in any Sprite Libraries in the project. Aborting the upgrade.");
                return 0;   
            }

            var combinedHash = SpriteLibrary.GetHashForCategoryAndEntry(categoryString, labelString);
            return combinedHash;
        }        
    }
}
