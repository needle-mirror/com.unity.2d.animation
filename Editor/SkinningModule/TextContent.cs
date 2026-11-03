using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal static class TextContent
    {
        // Undo
        public static readonly string setMode = "Set Mode";
        public static readonly string setTool = "Set Tool";
        public static readonly string pasteData = "Paste Data";
        public static readonly string generateGeometry = "Generate Geometry";
        public static readonly string generateWeights = "Generate Weights";
        public static readonly string normalizeWeights = "Normalize Weights";
        public static readonly string clearWeights = "Clear Weights";
        public static readonly string restorePose = "Restore Pose";
        public static readonly string pivotPose = "Pivot Pose";
        public static readonly string selection = "Selection";
        public static readonly string clearSelection = "Clear Selection";
        public static readonly string editWeights = "Edit Weights";
        public static readonly string boneName = "Bone Name";
        public static readonly string boneDepth = "Bone Depth";
        public static readonly string boneColor = "Bone Color";
        public static readonly string rotateBone = "Rotate Bone";
        public static readonly string moveBone = "Move Bone";
        public static readonly string colorBoneChanged = "Bone Color";
        public static readonly string freeMoveBone = "Free Move Bone";
        public static readonly string moveJoint = "Move Joint";
        public static readonly string moveEndPoint = "Move End Point";
        public static readonly string boneLength = "Bone Length";
        public static readonly string createBone = "Create Bone";
        public static readonly string splitBone = "Split Bone";
        public static readonly string removeBone = "Remove Bone";
        public static readonly string moveVertices = "Move Vertices";
        public static readonly string createVertex = "Create Vertex";
        public static readonly string createEdge = "Create Edge";
        public static readonly string splitEdge = "Split Edge";
        public static readonly string removeEdge = "Remove Edge";
        public static readonly string removeVertices = "Remove Vertices";
        public static readonly string selectionChange = "Selection Change";
        public static readonly string boneVisibility = "Bone Visibility";
        public static readonly string setParentBone = "Set Parent Bone";
        public static readonly string visibilityChange = "VisibilityChange";
        public static readonly string boneSelection = "Bone Selection";
        public static readonly string expandBones = "Expand Bones";
        public static readonly string meshVisibility = "Mesh Visibility";
        public static readonly string meshOpacity = "Mesh Opacity";
        public static readonly string opacityChange = "Opacity Change";
        public static readonly string spriteVisibility = "SpriteVisibility";
        public static readonly string visibilityTab = "Visibility Tab";
        public static readonly string addBoneInfluence = "Add Bone Influence";
        public static readonly string removeBoneInfluence = "Remove Bone Influence";
        public static readonly string reorderBoneInfluence = "Reorder Bone Influence";
        public static readonly string addSpriteInfluence = "Add Sprite Influence";
        public static readonly string removeSpriteInfluence = "Remove Sprite Influence";
        public static readonly string spriteSelection = "Sprite selection";
        public static readonly string pivotChanged = "Pivot changed";

        // Tooltips
        public static readonly string visibilityIconTooltip = L10n.Tr("Visibility tool");
        public static readonly string characterIconTooltip = L10n.Tr("Restore bind pose");
        public static readonly string spriteSheetIconTooltip = L10n.Tr("Switch between Sprite sheet and Character mode");
        public static readonly string copyTooltip = L10n.Tr("Copy");
        public static readonly string pasteTooltip = L10n.Tr("Paste");
        public static readonly string onTooltip = L10n.Tr("On");
        public static readonly string offTooltip = L10n.Tr("Off");
        public static readonly string addBoneInfluenceTooltip = L10n.Tr("Add selected bone to influence Sprite");
        public static readonly string removeBoneInfluenceTooltip = L10n.Tr("Remove selected bone from influencing Sprite");
        public static readonly string addSpriteInfluenceTooltip = L10n.Tr("Add selected Sprite to be influenced by bone");
        public static readonly string removeSpriteInfluenceTooltip = L10n.Tr("Remove selected Sprite from being influenced by bone");

        // Horizontal tool bar button txt
        public static readonly string visibilityIconText = L10n.Tr("Visibility");
        public static readonly string spriteSheetIconText = L10n.Tr("Sprite Sheet");
        public static readonly string copyText = L10n.Tr("Copy");
        public static readonly string pasteText = L10n.Tr("Paste");

        // Settings
        public static readonly string selectedOutlineColor = L10n.Tr("Selected Outline Color");
        public static readonly string hoveredOutlineColor = L10n.Tr("Hovered Outline Color");
        public static readonly string spriteOutlineSize = L10n.Tr("Sprite Outline Size");
        public static readonly string boneOutlineSize = L10n.Tr("Bone Outline Size");
        public static readonly string showSpriteMeshOverwriteWarning = L10n.Tr("Mesh Overwrite Warning");
        public static readonly string showSpriteMeshOverwriteWarningTip = L10n.Tr("Log warning message when Skinning Editor is overwriting custom outline mesh.");

        // Sprite Library
        public static readonly string categoryList = L10n.Tr("Category List");
        public static readonly string category = L10n.Tr("Category");
        public static readonly string label = L10n.Tr("Label");
        public static readonly string spriteLibraryCircularDependency = L10n.Tr("The Sprite Library can't be added because self-referencing is not allowed. Try another Sprite Library instead.");
        public static readonly string spriteLibraryNoAssetSelected = L10n.Tr("Select a Sprite Library Asset in your Project window.\n\nNew Sprite Library Assets can be created from the asset creation menu:\nAsset > Create > 2D > Sprite Library Asset");
        public static readonly string spriteCategoryColumnEmpty = L10n.Tr("To start creating categories drag and drop Sprites or Sprite Texture assets or select the '+' button.");
        public static readonly string spriteLabelColumnEmpty = L10n.Tr("To start creating labels drag and drop Sprites or Sprite Texture or select the '+' button.");
        public static readonly string spriteCategoryMultiSelect = L10n.Tr("Multiple Categories selected.");
        public static readonly string spriteCategoryNoSelection = L10n.Tr("No Categories selected.");
        public static readonly string spriteLibraryRevertMessage = L10n.Tr("There are some unsaved changes, are you sure you want to revert them?");
        public static readonly string spriteLibraryMainLibraryTooltip = L10n.Tr("This field is optional. By linking a Main Library, this Sprite Library becomes a Variant of the Main Library allowing it to reference all the Main Library’s Categories.");
        public static readonly string spriteLibraryCategoriesTooltip = L10n.Tr("A container to organize the Labels. The Category must be unique from other Categories in the same Sprite Library or Sprite Library hierarchy.");
        public static readonly string spriteLibraryLabelsTooltip = L10n.Tr("Label contains a Sprite reference. Name has to be unique.");
        public static readonly string spriteLibraryAddCategoryTooltip = L10n.Tr("Click here to add a new Category.");
        public static readonly string spriteLibraryAddCategoryTooltipNotAvailable = L10n.Tr("Cannot create new Category with search filter selected.");
        public static readonly string spriteLibraryAddLabelTooltip = L10n.Tr("Click here to add a new Label.");
        public static readonly string spriteLibraryAddLabelTooltipNotAvailable = L10n.Tr("Cannot create new Label with search filter selected.");
        public static readonly string spriteLibraryLocalCategoryTooltip = L10n.Tr("Local Categories exits only in this Sprite Library Asset.");
        public static readonly string spriteLibraryInheritedCategoryTooltip = L10n.Tr("Inherited Categories come from the Main Library Asset. Their Labels can be overriden in the Labels tab.");
        public static readonly string spriteLibraryCreateCategory = L10n.Tr("Create new Category");
        public static readonly string spriteLibraryRenameCategory = L10n.Tr("Rename Category");
        public static readonly string spriteLibraryDeleteCategories = L10n.Tr("Delete Selected Categories");
        public static readonly string spriteLibraryCreateLabel = L10n.Tr("Create new Label");
        public static readonly string spriteLibraryRenameLabel = L10n.Tr("Rename Label");
        public static readonly string spriteLibraryDeleteLabels = L10n.Tr("Delete Selected Labels");
        public static readonly string spriteLibraryShowLabel = L10n.Tr("Show Label Location");
        public static readonly string spriteLibraryRevertLabels = L10n.Tr("Revert Selected Overrides");
        public static readonly string spriteLibraryRevertAllLabels = L10n.Tr("Revert All Overrides");
        public static readonly string spriteLibrarySetMainLibrary = L10n.Tr("Set Main Library");
        public static readonly string spriteLibrarySelectCategories = L10n.Tr("Select Categories");
        public static readonly string spriteLibrarySelectLabels = L10n.Tr("Select Labels");
        public static readonly string spriteLibraryReorderCategories = L10n.Tr("Reorder Categories");
        public static readonly string spriteLibraryReorderLabels = L10n.Tr("Reorder Labels");
        public static readonly string spriteLibrarySetLabelSprite = L10n.Tr("Set Label's Sprite");
        public static readonly string spriteLibraryAddDataToCategories = L10n.Tr("Add data to Categories");
        public static readonly string spriteLibraryAddDataToLabels = L10n.Tr("Add data to Labels");
        public static readonly string spriteLibraryCreateNewAsset = L10n.Tr("Create new Sprite Library Asset");
        public static readonly string spriteLibraryCreateTitle = L10n.Tr("New Sprite Library Asset");
        public static readonly string spriteLibraryCreateMessage = L10n.Tr("Create a new Sprite Library Asset");

        // Sprite Resolver
        public static readonly string emptyCategory = L10n.Tr("Category is Empty");
        public static readonly string noCategory = L10n.Tr("No Category");
        public static readonly string spriteSwapSelectSpriteResolver = L10n.Tr("Select any Game Object(s) with the Sprite Resolver component in the Scene to start Sprite Swapping.");
        public static readonly string spriteSwapFilterDescription = L10n.Tr("Filters out single-label Sprite Resolvers");
        public static readonly string spriteSwapLockDescription = L10n.Tr("Locks the current selection");
        public static readonly string spriteSwapResetThumbnailSize = L10n.Tr("Resets the Label thumbnail to its default size");
        public static readonly string spriteSwapThumbnailSlider = L10n.Tr("Sets the Label thumbnail size");
        public static readonly string spriteSwapFilteredContent = L10n.Tr("Filtered Content");

        // Other
        public static readonly string savePopupTitle = L10n.Tr("Unsaved changes");
        public static readonly string savePopupMessage = L10n.Tr("There are some unsaved changes, would you like to save them?");
        public static readonly string savePopupOptionYes = L10n.Tr("Yes");
        public static readonly string savePopupOptionNo = L10n.Tr("No");
        public static readonly string generatingOutline = L10n.Tr("Generating Outline");
        public static readonly string triangulatingGeometry = L10n.Tr("Triangulating Geometry");
        public static readonly string subdividingGeometry = L10n.Tr("Subdividing Geometry");
        public static readonly string generatingWeights = L10n.Tr("Generating Weights");
        public static readonly string restorePoseLocalized = L10n.Tr("Restore Pose");
        public static readonly string vertexWeight = L10n.Tr("Vertex Weight");
        public static readonly string vertexWeightToolTip = L10n.Tr("Adjust bone weights for selected vertex");
        public static readonly string bone = L10n.Tr("Bone");
        public static readonly string boneTooltip = L10n.Tr("Show bone visibility tool");
        public static readonly string depth = L10n.Tr("Depth");
        public static readonly string color = L10n.Tr("Color");
        public static readonly string sprite = L10n.Tr("Sprite");
        public static readonly string spriteTooltip = L10n.Tr("Show sprite visibility tool");
        public static readonly string name = L10n.Tr("Name");
        public static readonly string none = L10n.Tr("None");
        public static readonly string size = L10n.Tr("Size");
        public static readonly string noSpriteSelected = L10n.Tr("No sprite selected");
        public static readonly string weightSlider = L10n.Tr("Weight Slider");
        public static readonly string weightBrush = L10n.Tr("Weight Brush");
        public static readonly string generateAll = L10n.Tr("Generate All");
        public static readonly string generate = L10n.Tr("Generate");
        public static readonly string mode = L10n.Tr("Mode");
        public static readonly string modeTooltip = L10n.Tr("Different operation mode for weight adjustment");
        public static readonly string boneToolTip = L10n.Tr("The bone that is affecting");
        public static readonly string pivot = L10n.Tr("Pivot");

        public static readonly string noBoneSelected = L10n.Tr("No bone selected");
        public static readonly string boneInfluences = L10n.Tr("Bone Influences");
        public static readonly string influencedSprites = L10n.Tr("Sprite Influences");

        // Error messages
        public static readonly string pasteSpritesScaledDifferentPPU = L10n.Tr("Pasted sprites have been scaled due to different Pixels Per Unit values (from {0} to {1}). If the result is unintended, ensure both sprites have matching Pixels Per Unit values before copying.");
        public static readonly string spriteMeshOverwriteWarning = L10n.Tr("{0} has custom outline defined. The Sprite's mesh will be overwritten by the mesh defined in Skinning Editor.");
        public static readonly string boneWeightsNotSumZeroWarning = L10n.Tr("Sprite {0} contains bone weights which sum zero or are not normalized. To avoid visual artifacts please consider fixing them.");

        // Bone overlay (Scene View)
        public static readonly string boneOverlayName = L10n.Tr("Bone");
        public static readonly string boneOverlayColorLabel = L10n.Tr("Color");
    }
}
