using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace DualContouring.ScalarField.Debug.Editor
{
    [Overlay(typeof(SceneView), "Scalar Field Visualization")]
    public class ScalarFieldVisualizationOverlay : Overlay
    {
        private readonly Dictionary<Entity, Button> _entityButtons = new Dictionary<Entity, Button>();
        private Label _headerLabel;
        private VisualElement _root;
        private ScrollView _scrollView;
        private SliderInt _selectedCellMinXField;
        private SliderInt _selectedCellMinYField;
        private SliderInt _selectedCellMinZField;
        private SliderInt _selectedCellMaxXField;
        private SliderInt _selectedCellMaxYField;
        private SliderInt _selectedCellMaxZField;
        private VisualElement _selectedPanel;
        private Label _selectedTitleLabel;
        private Toggle _visualizationToggle;

        public override VisualElement CreatePanelContent()
        {
            _root = new VisualElement();
            _root.style.paddingLeft = 10;
            _root.style.paddingRight = 10;
            _root.style.paddingTop = 10;
            _root.style.paddingBottom = 10;
            _root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            _root.style.borderBottomLeftRadius = 5;
            _root.style.borderBottomRightRadius = 5;
            _root.style.borderTopLeftRadius = 5;
            _root.style.borderTopRightRadius = 5;
            _root.style.minWidth = 250;
            _root.style.maxHeight = 500;

            // Header
            _headerLabel = new Label("Scalar Fields");
            _headerLabel.style.fontSize = 16;
            _headerLabel.style.color = Color.white;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _headerLabel.style.marginBottom = 5;
            _root.Add(_headerLabel);

            // ScrollView pour la liste
            _scrollView = new ScrollView();
            _scrollView.style.flexGrow = 1;
            _root.Add(_scrollView);

            // Séparateur
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            separator.style.marginTop = 5;
            separator.style.marginBottom = 5;
            _root.Add(separator);

            // Panel "Selected Scalar Field"
            _selectedPanel = new VisualElement();
            _selectedPanel.style.paddingLeft = 5;
            _selectedPanel.style.paddingRight = 5;
            _selectedPanel.style.paddingTop = 5;
            _selectedPanel.style.paddingBottom = 5;
            _selectedPanel.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            _selectedPanel.style.borderBottomLeftRadius = 3;
            _selectedPanel.style.borderBottomRightRadius = 3;
            _selectedPanel.style.borderTopLeftRadius = 3;
            _selectedPanel.style.borderTopRightRadius = 3;
            _selectedPanel.style.display = DisplayStyle.None; // Caché par défaut

            // Toggle pour la visualisation
            _visualizationToggle = new Toggle("Enable Visualization");
            _visualizationToggle.style.color = Color.white;
            _visualizationToggle.style.fontSize = 11;
            _visualizationToggle.style.marginBottom = 5;
            _visualizationToggle.RegisterValueChangedCallback(evt => OnVisualizationToggleChanged(evt.newValue));
            _root.Add(_visualizationToggle);

            _selectedTitleLabel = new Label("Selected Scalar Field");
            _selectedTitleLabel.style.fontSize = 12;
            _selectedTitleLabel.style.color = new Color(0.5f, 0.9f, 1f);
            _selectedTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _selectedTitleLabel.style.marginBottom = 5;
            _selectedPanel.Add(_selectedTitleLabel);

            // Min Cell
            var minCellLabel = new Label("Min Cell:");
            minCellLabel.style.fontSize = 10;
            minCellLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            minCellLabel.style.marginBottom = 2;
            _selectedPanel.Add(minCellLabel);

            var minCellContainer = new VisualElement();
            minCellContainer.style.flexDirection = FlexDirection.Column;
            minCellContainer.style.marginBottom = 5;

            // Min X
            var minXContainer = new VisualElement();
            minXContainer.style.flexDirection = FlexDirection.Row;
            minXContainer.style.marginBottom = 2;
            var minXLabel = new Label("X:");
            minXLabel.style.fontSize = 10;
            minXLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            minXLabel.style.width = 15;
            minXLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            minXContainer.Add(minXLabel);
            _selectedCellMinXField = new SliderInt(0, 10);
            _selectedCellMinXField.style.flexGrow = 1;
            _selectedCellMinXField.showInputField = true;
            _selectedCellMinXField.RegisterValueChangedCallback(evt => OnSelectedCellChanged());
            minXContainer.Add(_selectedCellMinXField);
            minCellContainer.Add(minXContainer);

            // Min Y
            var minYContainer = new VisualElement();
            minYContainer.style.flexDirection = FlexDirection.Row;
            minYContainer.style.marginBottom = 2;
            var minYLabel = new Label("Y:");
            minYLabel.style.fontSize = 10;
            minYLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            minYLabel.style.width = 15;
            minYLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            minYContainer.Add(minYLabel);
            _selectedCellMinYField = new SliderInt(0, 10);
            _selectedCellMinYField.style.flexGrow = 1;
            _selectedCellMinYField.showInputField = true;
            _selectedCellMinYField.RegisterValueChangedCallback(evt => OnSelectedCellChanged());
            minYContainer.Add(_selectedCellMinYField);
            minCellContainer.Add(minYContainer);

            // Min Z
            var minZContainer = new VisualElement();
            minZContainer.style.flexDirection = FlexDirection.Row;
            var minZLabel = new Label("Z:");
            minZLabel.style.fontSize = 10;
            minZLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            minZLabel.style.width = 15;
            minZLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            minZContainer.Add(minZLabel);
            _selectedCellMinZField = new SliderInt(0, 10);
            _selectedCellMinZField.style.flexGrow = 1;
            _selectedCellMinZField.showInputField = true;
            _selectedCellMinZField.RegisterValueChangedCallback(evt => OnSelectedCellChanged());
            minZContainer.Add(_selectedCellMinZField);
            minCellContainer.Add(minZContainer);

            _selectedPanel.Add(minCellContainer);

            // Max Cell
            var maxCellLabel = new Label("Max Cell:");
            maxCellLabel.style.fontSize = 10;
            maxCellLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            maxCellLabel.style.marginBottom = 2;
            _selectedPanel.Add(maxCellLabel);

            var maxCellContainer = new VisualElement();
            maxCellContainer.style.flexDirection = FlexDirection.Column;
            maxCellContainer.style.marginBottom = 3;

            // Max X
            var maxXContainer = new VisualElement();
            maxXContainer.style.flexDirection = FlexDirection.Row;
            maxXContainer.style.marginBottom = 2;
            var maxXLabel = new Label("X:");
            maxXLabel.style.fontSize = 10;
            maxXLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            maxXLabel.style.width = 15;
            maxXLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            maxXContainer.Add(maxXLabel);
            _selectedCellMaxXField = new SliderInt(0, 10);
            _selectedCellMaxXField.style.flexGrow = 1;
            _selectedCellMaxXField.showInputField = true;
            _selectedCellMaxXField.RegisterValueChangedCallback(evt => OnSelectedCellChanged());
            maxXContainer.Add(_selectedCellMaxXField);
            maxCellContainer.Add(maxXContainer);

            // Max Y
            var maxYContainer = new VisualElement();
            maxYContainer.style.flexDirection = FlexDirection.Row;
            maxYContainer.style.marginBottom = 2;
            var maxYLabel = new Label("Y:");
            maxYLabel.style.fontSize = 10;
            maxYLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            maxYLabel.style.width = 15;
            maxYLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            maxYContainer.Add(maxYLabel);
            _selectedCellMaxYField = new SliderInt(0, 10);
            _selectedCellMaxYField.style.flexGrow = 1;
            _selectedCellMaxYField.showInputField = true;
            _selectedCellMaxYField.RegisterValueChangedCallback(evt => OnSelectedCellChanged());
            maxYContainer.Add(_selectedCellMaxYField);
            maxCellContainer.Add(maxYContainer);

            // Max Z
            var maxZContainer = new VisualElement();
            maxZContainer.style.flexDirection = FlexDirection.Row;
            var maxZLabel = new Label("Z:");
            maxZLabel.style.fontSize = 10;
            maxZLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            maxZLabel.style.width = 15;
            maxZLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            maxZContainer.Add(maxZLabel);
            _selectedCellMaxZField = new SliderInt(0, 10);
            _selectedCellMaxZField.style.flexGrow = 1;
            _selectedCellMaxZField.showInputField = true;
            _selectedCellMaxZField.RegisterValueChangedCallback(evt => OnSelectedCellChanged());
            maxZContainer.Add(_selectedCellMaxZField);
            maxCellContainer.Add(maxZContainer);

            _selectedPanel.Add(maxCellContainer);
            _root.Add(_selectedPanel);

            // S'abonner aux mises à jour
            EditorApplication.update += OnEditorUpdate;

            RefreshEntityList();

            return _root;
        }

        public override void OnWillBeDestroyed()
        {
            base.OnWillBeDestroyed();
            EditorApplication.update -= OnEditorUpdate;
            _entityButtons.Clear();
        }

        private void OnEditorUpdate()
        {
            if (_scrollView != null)
            {
                RefreshEntityList();
            }
        }

        private void RefreshEntityList()
        {
            if (_scrollView == null)
            {
                return;
            }

            // Nettoyer tous les labels informatifs (non-boutons)
            List<VisualElement> childrenToRemove = new List<VisualElement>();
            foreach (VisualElement child in _scrollView.Children())
            {
                if (child is Label)
                {
                    childrenToRemove.Add(child);
                }
            }

            foreach (VisualElement child in childrenToRemove)
            {
                _scrollView.Remove(child);
            }

            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            {
                var noDataLabel = new Label("▶ Démarrez le Play Mode");
                noDataLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                noDataLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noDataLabel.style.marginTop = 10;
                _scrollView.Add(noDataLabel);

                // Cacher le panel de sélection
                if (_selectedPanel != null)
                {
                    _selectedPanel.style.display = DisplayStyle.None;
                }

                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(typeof(ScalarFieldInfos));
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

            // Mettre à jour le header avec le compte
            if (_headerLabel != null)
            {
                _headerLabel.text = $"Scalar Fields ({entities.Length})";
            }

            // Supprimer les boutons des entités qui n'existent plus
            List<Entity> entitiesToRemove = new List<Entity>();
            foreach (KeyValuePair<Entity, Button> kvp in _entityButtons)
            {
                bool stillExists = false;
                for (int i = 0; i < entities.Length; i++)
                {
                    if (kvp.Key == entities[i])
                    {
                        stillExists = true;
                        break;
                    }
                }

                if (!stillExists)
                {
                    _scrollView.Remove(kvp.Value);
                    entitiesToRemove.Add(kvp.Key);
                }
            }

            foreach (Entity entity in entitiesToRemove)
            {
                _entityButtons.Remove(entity);
            }

            // Ajouter les boutons pour les nouvelles entités
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!_entityButtons.ContainsKey(entity))
                {
                    CreateButtonForEntity(entity, i);
                }
                else
                {
                    UpdateButtonText(entity, _entityButtons[entity]);
                }
            }

            // Si aucune entité
            if (entities.Length == 0)
            {
                var noDataLabel = new Label("Aucune entité trouvée");
                noDataLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                noDataLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noDataLabel.style.marginTop = 10;
                _scrollView.Add(noDataLabel);
            }

            entities.Dispose();

            // Mettre à jour le panel de sélection
            UpdateSelectedPanel();
        }

        private void UpdateSelectedPanel()
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null || _selectedPanel == null)
            {
                if (_selectedPanel != null)
                {
                    _selectedPanel.style.display = DisplayStyle.None;
                }

                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(typeof(ScalarFieldInfos), typeof(ScalarFieldSelected));
            NativeArray<Entity> selectedEntities = query.ToEntityArray(Allocator.Temp);

            if (selectedEntities.Length == 0)
            {
                _selectedPanel.style.display = DisplayStyle.None;
                selectedEntities.Dispose();
                return;
            }

            // Afficher le panel
            _selectedPanel.style.display = DisplayStyle.Flex;

            Entity selectedEntity = selectedEntities[0];
            selectedEntities.Dispose();

            // Mettre à jour le titre
            _selectedTitleLabel.text = $"Selected: Scalar Field {selectedEntity.Index}";

            // Récupérer la GridSize pour mettre à jour les limites des sliders
            if (entityManager.HasComponent<ScalarFieldInfos>(selectedEntity))
            {
                var scalarFieldInfos = entityManager.GetComponentData<ScalarFieldInfos>(selectedEntity);

                // Mettre à jour les limites des sliders (0 à GridSize - 1)
                _selectedCellMinXField.lowValue = 0;
                _selectedCellMinXField.highValue = Mathf.Max(0, scalarFieldInfos.GridSize.x - 1);

                _selectedCellMinYField.lowValue = 0;
                _selectedCellMinYField.highValue = Mathf.Max(0, scalarFieldInfos.GridSize.y - 1);

                _selectedCellMinZField.lowValue = 0;
                _selectedCellMinZField.highValue = Mathf.Max(0, scalarFieldInfos.GridSize.z - 1);

                _selectedCellMaxXField.lowValue = 0;
                _selectedCellMaxXField.highValue = Mathf.Max(0, scalarFieldInfos.GridSize.x - 1);

                _selectedCellMaxYField.lowValue = 0;
                _selectedCellMaxYField.highValue = Mathf.Max(0, scalarFieldInfos.GridSize.y - 1);

                _selectedCellMaxZField.lowValue = 0;
                _selectedCellMaxZField.highValue = Mathf.Max(0, scalarFieldInfos.GridSize.z - 1);
            }

            // Récupérer et afficher la Selected Cell
            if (entityManager.HasComponent<ScalarFieldSelectedCell>(selectedEntity))
            {
                var selectedCell = entityManager.GetComponentData<ScalarFieldSelectedCell>(selectedEntity);

                // Mettre à jour les champs sans déclencher l'événement
                _selectedCellMinXField.SetValueWithoutNotify(selectedCell.Min.x);
                _selectedCellMinYField.SetValueWithoutNotify(selectedCell.Min.y);
                _selectedCellMinZField.SetValueWithoutNotify(selectedCell.Min.z);

                _selectedCellMaxXField.SetValueWithoutNotify(selectedCell.Max.x);
                _selectedCellMaxYField.SetValueWithoutNotify(selectedCell.Max.y);
                _selectedCellMaxZField.SetValueWithoutNotify(selectedCell.Max.z);
            }
        }

        private void OnSelectedCellChanged()
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(typeof(ScalarFieldInfos), typeof(ScalarFieldSelected));
            NativeArray<Entity> selectedEntities = query.ToEntityArray(Allocator.Temp);

            if (selectedEntities.Length == 0)
            {
                selectedEntities.Dispose();
                return;
            }

            Entity selectedEntity = selectedEntities[0];
            selectedEntities.Dispose();

            // Mettre à jour le composant SelectedCell
            if (entityManager.HasComponent<ScalarFieldSelectedCell>(selectedEntity))
            {
                var newSelectedCell = new ScalarFieldSelectedCell
                {
                    Min = new int3(
                        _selectedCellMinXField.value,
                        _selectedCellMinYField.value,
                        _selectedCellMinZField.value
                    ),
                    Max = new int3(
                        _selectedCellMaxXField.value,
                        _selectedCellMaxYField.value,
                        _selectedCellMaxZField.value
                    )
                };
                entityManager.SetComponentData(selectedEntity, newSelectedCell);
                SceneView.RepaintAll();
            }
        }

        private void CreateButtonForEntity(Entity entity, int index)
        {
            var button = new Button(() => OnEntityButtonClicked(entity));
            button.name = $"ScalarFieldButton_{index}";
            UpdateButtonText(entity, button);

            button.style.height = 28;
            button.style.marginBottom = 2;

            _scrollView.Add(button);
            _entityButtons[entity] = button;
        }

        private void OnEntityButtonClicked(Entity entity)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!entityManager.Exists(entity))
            {
                return;
            }

            bool wasSelected = entityManager.HasComponent<ScalarFieldSelected>(entity);

            // Désélectionner TOUS les Scalar Fields
            EntityQuery query = entityManager.CreateEntityQuery(typeof(ScalarFieldInfos));
            NativeArray<Entity> allEntities = query.ToEntityArray(Allocator.Temp);

            foreach (Entity e in allEntities)
            {
                if (entityManager.HasComponent<ScalarFieldSelected>(e))
                {
                    entityManager.RemoveComponent<ScalarFieldSelected>(e);
                }
            }

            allEntities.Dispose();

            // Si l'entité n'était pas sélectionnée, la sélectionner maintenant
            // (comportement de toggle, mais un seul à la fois)
            if (!wasSelected)
            {
                entityManager.AddComponent<ScalarFieldSelected>(entity);
            }

            // Mettre à jour l'affichage de TOUS les boutons
            foreach (KeyValuePair<Entity, Button> kvp in _entityButtons)
            {
                UpdateButtonText(kvp.Key, kvp.Value);
            }

            SceneView.RepaintAll();
        }

        private void UpdateButtonText(Entity entity, Button button)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null || button == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!entityManager.Exists(entity))
            {
                return;
            }

            bool isSelected = entityManager.HasComponent<ScalarFieldSelected>(entity);
            string icon = isSelected ? "✓" : "○";
            button.text = $"{icon} Scalar Field {entity.Index}";

            // Changer le style selon la sélection
            if (isSelected)
            {
                button.style.backgroundColor = new Color(0.3f, 0.6f, 0.9f, 0.5f);
            }
            else
            {
                button.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            }
        }

        private void OnVisualizationToggleChanged(bool newValue)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Récupérer le singleton ScalarFieldVisualizationOptions
            EntityQuery query = entityManager.CreateEntityQuery(typeof(ScalarFieldVisualizationOptions));
            NativeArray<Entity> singletonEntities = query.ToEntityArray(Allocator.Temp);

            if (singletonEntities.Length > 0)
            {
                Entity singletonEntity = singletonEntities[0];
                var options = entityManager.GetComponentData<ScalarFieldVisualizationOptions>(singletonEntity);
                options.Enabled = newValue;
                entityManager.SetComponentData(singletonEntity, options);
            }

            singletonEntities.Dispose();
        }
    }
}