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
        private SliderInt _selectedCellXField;
        private SliderInt _selectedCellYField;
        private SliderInt _selectedCellZField;
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

            // Champs pour Selected Cell
            var cellLabel = new Label("Selected Cell:");
            cellLabel.style.fontSize = 10;
            cellLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            cellLabel.style.marginBottom = 2;
            _selectedPanel.Add(cellLabel);

            var cellContainer = new VisualElement();
            cellContainer.style.flexDirection = FlexDirection.Column;
            cellContainer.style.marginBottom = 3;

            // Slider pour X
            var xContainer = new VisualElement();
            xContainer.style.flexDirection = FlexDirection.Row;
            xContainer.style.marginBottom = 2;
            var xLabel = new Label("X:");
            xLabel.style.fontSize = 10;
            xLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            xLabel.style.width = 15;
            xLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            xContainer.Add(xLabel);
            _selectedCellXField = new SliderInt(0, 10);
            _selectedCellXField.style.flexGrow = 1;
            _selectedCellXField.showInputField = true;
            _selectedCellXField.RegisterValueChangedCallback(evt => OnSelectedCellChanged());
            xContainer.Add(_selectedCellXField);
            cellContainer.Add(xContainer);

            // Slider pour Y
            var yContainer = new VisualElement();
            yContainer.style.flexDirection = FlexDirection.Row;
            yContainer.style.marginBottom = 2;
            var yLabel = new Label("Y:");
            yLabel.style.fontSize = 10;
            yLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            yLabel.style.width = 15;
            yLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            yContainer.Add(yLabel);
            _selectedCellYField = new SliderInt(0, 10);
            _selectedCellYField.style.flexGrow = 1;
            _selectedCellYField.showInputField = true;
            _selectedCellYField.RegisterValueChangedCallback(evt => OnSelectedCellChanged());
            yContainer.Add(_selectedCellYField);
            cellContainer.Add(yContainer);

            // Slider pour Z
            var zContainer = new VisualElement();
            zContainer.style.flexDirection = FlexDirection.Row;
            var zLabel = new Label("Z:");
            zLabel.style.fontSize = 10;
            zLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            zLabel.style.width = 15;
            zLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            zContainer.Add(zLabel);
            _selectedCellZField = new SliderInt(0, 10);
            _selectedCellZField.style.flexGrow = 1;
            _selectedCellZField.showInputField = true;
            _selectedCellZField.RegisterValueChangedCallback(evt => OnSelectedCellChanged());
            zContainer.Add(_selectedCellZField);
            cellContainer.Add(zContainer);

            _selectedPanel.Add(cellContainer);
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
                _selectedCellXField.lowValue = 0;
                _selectedCellXField.highValue = Mathf.Max(0, scalarFieldInfos.GridSize.x - 1);

                _selectedCellYField.lowValue = 0;
                _selectedCellYField.highValue = Mathf.Max(0, scalarFieldInfos.GridSize.y - 1);

                _selectedCellZField.lowValue = 0;
                _selectedCellZField.highValue = Mathf.Max(0, scalarFieldInfos.GridSize.z - 1);
            }

            // Récupérer et afficher la Selected Cell
            if (entityManager.HasComponent<SelectedCell>(selectedEntity))
            {
                var selectedCell = entityManager.GetComponentData<SelectedCell>(selectedEntity);

                // Mettre à jour les champs sans déclencher l'événement
                _selectedCellXField.SetValueWithoutNotify(selectedCell.Value.x);
                _selectedCellYField.SetValueWithoutNotify(selectedCell.Value.y);
                _selectedCellZField.SetValueWithoutNotify(selectedCell.Value.z);
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
            if (entityManager.HasComponent<SelectedCell>(selectedEntity))
            {
                var newSelectedCell = new SelectedCell
                {
                    Value = new int3(
                        _selectedCellXField.value,
                        _selectedCellYField.value,
                        _selectedCellZField.value
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