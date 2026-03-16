using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;


namespace Shtl.Mvvm.Editor
{
    [CustomEditor(typeof(DevWidget))]
    public class DevWidgetEditor : UnityEditor.Editor
    {
        private DevWidget _entry;
        private ViewModelDrawer _viewModelDrawer;
        private VisualElement _dynamicContainer;
        private bool _showUnsupportedFields;

        private readonly JsonSerializerSettings _settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
        };

        public override VisualElement CreateInspectorGUI()
        {
            _entry = (DevWidget)target;
            _viewModelDrawer = new ViewModelDrawer();
            _viewModelDrawer.OnStructureChanged = RebuildDynamicContent;

            var root = new VisualElement();

            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    var propField = new PropertyField(iterator.Copy());
                    if (iterator.propertyPath == "m_Script")
                    {
                        propField.SetEnabled(false);
                    }
                    root.Add(propField);
                } while (iterator.NextVisible(false));
            }

            root.Add(CreateSeparator());

            var toggle = new Toggle("Show unsupported fields") { value = _showUnsupportedFields };
            toggle.RegisterValueChangedCallback(evt =>
            {
                _showUnsupportedFields = evt.newValue;
                _viewModelDrawer.ShowUnsupportedFields = evt.newValue;
                RebuildDynamicContent();
            });
            root.Add(toggle);

            _dynamicContainer = new VisualElement();
            root.Add(_dynamicContainer);

            root.TrackSerializedObjectValue(serializedObject, _ => RebuildDynamicContent());
            RebuildDynamicContent();

            return root;
        }

        private void RebuildDynamicContent()
        {
            _dynamicContainer.Clear();
            serializedObject.Update();

            var prefab = serializedObject.FindProperty("_uiPrefab");
            if (prefab.objectReferenceValue == null)
            {
                _dynamicContainer.Add(new HelpBox("_uiPrefab must be assigned", HelpBoxMessageType.Warning));
                return;
            }

            var prefabButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            prefabButtons.Add(new Button(() =>
            {
                _entry.OpenPrefab();
                RebuildDynamicContent();
            }) { text = "Open prefab", style = { flexGrow = 1 } });
            prefabButtons.Add(new Button(() =>
            {
                _entry.ClosePrefab();
                RebuildDynamicContent();
            }) { text = "Close prefab", style = { flexGrow = 1 } });
            _dynamicContainer.Add(prefabButtons);

            _dynamicContainer.Add(CreateSpacer(8));

            var saveLoadButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var saveBtn = new Button(SaveViewModel) { text = "Save view model", style = { flexGrow = 1 } };
            var loadBtn = new Button(() =>
            {
                LoadViewModel();
                RebuildDynamicContent();
            }) { text = "Load view model", style = { flexGrow = 1 } };
            saveBtn.SetEnabled(_entry.ViewModel != null);
            loadBtn.SetEnabled(_entry.ViewModel != null);
            saveLoadButtons.Add(saveBtn);
            saveLoadButtons.Add(loadBtn);
            _dynamicContainer.Add(saveLoadButtons);

            _dynamicContainer.Add(CreateSpacer(8));

            if (_entry.WidgetViewComponent == null)
            {
                _dynamicContainer.Add(new HelpBox(
                    "_uiPrefab is missing a component inherited from AbstractWidgetView<T>, or it has not been initialized yet",
                    HelpBoxMessageType.Warning));
                return;
            }

            if (_entry.ViewModelType == null)
            {
                _dynamicContainer.Add(new HelpBox(
                    "Unable to determine type T for AbstractWidgetView<T>",
                    HelpBoxMessageType.Error));
                return;
            }

            _dynamicContainer.Add(_viewModelDrawer.BuildViewModelElement(_entry.ViewModelType, _entry.ViewModel));
        }

        private void SaveViewModel()
        {
            if (_entry.ViewModel == null)
            {
                return;
            }

            var path = EditorUtility.SaveFilePanel("Save view model", "Assets", "ViewModel", "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var json = JsonConvert.SerializeObject(_entry.ViewModel, _settings);
            File.WriteAllText(path, json);
        }

        private void LoadViewModel()
        {
            var path = EditorUtility.OpenFilePanel("Load view model", "Assets", "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            var viewModel = JsonConvert.DeserializeObject(json, _entry.ViewModelType, _settings);
            _entry.UpdateViewModel(viewModel);
        }

        private static VisualElement CreateSeparator()
        {
            return new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = new Color(0.5f, 0.5f, 0.5f),
                    marginTop = 4,
                    marginBottom = 4
                }
            };
        }

        private static VisualElement CreateSpacer(float height)
        {
            return new VisualElement { style = { height = height } };
        }
    }
}
