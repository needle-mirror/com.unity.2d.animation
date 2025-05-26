using UnityEditor.U2D.Common;
using UnityEngine.Assertions;
using UnityEngine.U2D.Common;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation
{
#if ENABLE_UXML_SERIALIZED_DATA
    [UxmlElement]
#endif
    internal partial class Toolbar : VisualElement
    {
        private const string k_UssPath = "SkinningModule/ToolbarStyle.uss";

#if ENABLE_UXML_TRAITS
        public class ToolbarFactory : UxmlFactory<Toolbar, ToolbarUxmlTraits> { }

        public class ToolbarUxmlTraits : UxmlTraits { }
#endif

        protected ShortcutUtility m_ShortcutUtility;

        protected static Toolbar GetClone(string uxmlPath, string toolbarId)
        {
            VisualTreeAsset visualTree = ResourceLoader.Load<VisualTreeAsset>(uxmlPath);
            return visualTree.CloneTree().Q<Toolbar>(toolbarId);
        }

        public Toolbar()
        {
            AddToClassList("Toolbar");
            styleSheets.Add(ResourceLoader.Load<StyleSheet>(k_UssPath));
            if (EditorGUIUtility.isProSkin)
                AddToClassList("Dark");
        }

        public void SetButtonChecked(Button toCheck)
        {
            UQueryBuilder<Button> buttons = this.Query<Button>();
            buttons.ForEach((button) => { button.SetChecked(button == toCheck); });
        }

        protected void SetButtonChecked(Button button, bool check)
        {
            if (button.IsChecked() != check)
            {
                if (check)
                {
                    button.AddToClassList("Checked");
                    button.Focus();
                }
                else
                    button.RemoveFromClassList("Checked");

                button.SetChecked(check);
            }
        }

        public void CollapseToolBar(bool collapse)
        {
            if (collapse)
                AddToClassList("Collapse");
            else
                RemoveFromClassList("Collapse");
        }

        protected void RestoreButtonTooltips(string uxmlPath, string toolbarId)
        {
            Toolbar clone = GetClone(uxmlPath, toolbarId);
            System.Collections.Generic.List<Button> clonedButtons = clone.Query<Button>().ToList();
            System.Collections.Generic.List<Button> originalButtons = this.Query<Button>().ToList();

            Assert.AreEqual(originalButtons.Count, clonedButtons.Count);
            for (int i = 0; i < clonedButtons.Count; ++i)
            {
                originalButtons[i].tooltip = clonedButtons[i].tooltip;
                originalButtons[i].LocalizeTextInChildren();
            }
        }
    }
}
