using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;


namespace Shtl.Mvvm.Editor
{
    public class ViewModelViewerWindow : EditorWindow
    {
        private class VoidViewModel : AbstractViewModel { }

        private static readonly Regex RootPattern = new(@"(?:Window|Widget)View$");

        private const string GUI_GAME_OBJECT_NAME = "Gui";

        private readonly HashSet<AbstractViewModel> _selectedViewModels = new();
        private readonly Dictionary<AbstractViewModel, ViewModelDrawer> _drawerPerViewModel = new();
        private List<AbstractViewModel> _lastWidgets = new();

        private bool _showUnsupportedFields;

        private static readonly Dictionary<Type, PropertyInfo> _typeToProperty = new();

        private VisualElement _selectorContainer;
        private VisualElement _viewModelContainer;

        private static bool IsRootWidgetView(string typeName) => RootPattern.IsMatch(typeName);

        [MenuItem("Window/ViewModel Viewer")]
        public static void ShowWindow() => GetWindow<ViewModelViewerWindow>("ViewModel Viewer");

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var toggle = new Toggle("Show unsupported fields");
            toggle.RegisterValueChangedCallback(evt =>
            {
                _showUnsupportedFields = evt.newValue;
                RebuildViewModelDisplay();
            });
            root.Add(toggle);

            _selectorContainer = new VisualElement();
            root.Add(_selectorContainer);

            root.Add(new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = new Color(0.5f, 0.5f, 0.5f),
                    marginTop = 4,
                    marginBottom = 4
                }
            });

            var scrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { flexGrow = 1 }
            };
            _viewModelContainer = new VisualElement();
            scrollView.Add(_viewModelContainer);
            root.Add(scrollView);
        }

        private void OnEnable() => EditorApplication.update += OnEditorUpdate;
        private void OnDisable() => EditorApplication.update -= OnEditorUpdate;

        private void OnEditorUpdate()
        {
            if (_viewModelContainer == null)
            {
                return;
            }

            UpdateSelector();

            if (_selectedViewModels.Count == 0)
            {
                return;
            }

            var needsRebuild = false;
            foreach (var vm in _selectedViewModels)
            {
                if (_drawerPerViewModel.TryGetValue(vm, out var drawer) && drawer.UpdateValues())
                {
                    needsRebuild = true;
                }
            }

            if (needsRebuild)
            {
                RebuildViewModelDisplay();
            }
        }

        private void UpdateSelector()
        {
            var widgets = CollectActiveViewModels();
            if (widgets.SequenceEqual(_lastWidgets))
            {
                return;
            }

            _lastWidgets = widgets;
            RebuildSelector(widgets);
        }

        private void RebuildSelector(List<AbstractViewModel> widgets)
        {
            _selectorContainer.Clear();

            // Remove stale entries no longer present in scene
            _selectedViewModels.RemoveWhere(vm => !widgets.Contains(vm));
            foreach (var stale in _drawerPerViewModel.Keys.Where(vm => !widgets.Contains(vm)).ToList())
            {
                _drawerPerViewModel.Remove(stale);
            }

            if (widgets.Count <= 0)
            {
                RebuildViewModelDisplay();
                return;
            }

            foreach (var widget in widgets)
            {
                var isSelected = _selectedViewModels.Contains(widget);
                var capturedWidget = widget;
                var widgetToggle = new Toggle(widget.GetType().Name) { value = isSelected };
                widgetToggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        _selectedViewModels.Add(capturedWidget);
                    }
                    else
                    {
                        _selectedViewModels.Remove(capturedWidget);
                        _drawerPerViewModel.Remove(capturedWidget);
                    }
                    RebuildViewModelDisplay();
                });
                _selectorContainer.Add(widgetToggle);
            }

            if (widgets.Count == 1)
            {
                _selectedViewModels.Add(widgets[0]);
                RebuildViewModelDisplay();
            }
        }

        private void RebuildViewModelDisplay()
        {
            _viewModelContainer.Clear();
            _drawerPerViewModel.Clear();

            if (_selectedViewModels.Count == 0)
            {
                _viewModelContainer.Add(new HelpBox("No active view models found", HelpBoxMessageType.Info));
                return;
            }

            foreach (var vm in _selectedViewModels)
            {
                var drawer = new ViewModelDrawer(false)
                {
                    ShowUnsupportedFields = _showUnsupportedFields
                };
                _drawerPerViewModel[vm] = drawer;
                _viewModelContainer.Add(drawer.BuildViewModelElement(vm.GetType(), vm));
            }
        }

        private List<AbstractViewModel> CollectActiveViewModels()
        {
            var scene = SceneManager.GetActiveScene();
            var gui = scene.GetRootGameObjects().FirstOrDefault(x => x.name == GUI_GAME_OBJECT_NAME);
            if (gui == null)
            {
                return new List<AbstractViewModel>();
            }

            return gui.GetComponentsInChildren<MonoBehaviour>()
                .Where(x => IsSubclassOfAbstractWidgetView(x.GetType().BaseType) && IsRootWidgetView(x.GetType().Name))
                .Select(x =>
                {
                    var property = GetViewModelProp(x);
                    return property?.GetValue(x) as AbstractViewModel;
                })
                .Where(x => x != null)
                .ToList();
        }

        private static PropertyInfo GetViewModelProp(MonoBehaviour x)
        {
            if (!_typeToProperty.TryGetValue(x.GetType(), out var property))
            {
                property = x.GetType().GetProperty(
                    AbstractWidgetView<VoidViewModel>.ViewModelPropertyName,
                    BindingFlags.Public | BindingFlags.Instance);
                _typeToProperty[x.GetType()] = property;
            }
            return property;
        }

        private static bool IsSubclassOfAbstractWidgetView(Type type)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AbstractWidgetView<>))
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }
    }
}
