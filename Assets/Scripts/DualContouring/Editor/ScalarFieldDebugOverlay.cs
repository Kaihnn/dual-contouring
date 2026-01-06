using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace DualContouring.Editor
{
    /// <summary>
    /// Overlay qui affiche la liste des entités avec ScalarField dans la SceneView
    /// Compatible Unity 2021.2+
    /// </summary>
    [Overlay(typeof(SceneView), "Scalar Field Debug")]
    public class ScalarFieldDebugOverlay : Overlay
    {
        private VisualElement _root;
        private ScrollView _scrollView;
        private Label _headerLabel;
        private Dictionary<Entity, Button> _entityButtons = new Dictionary<Entity, Button>();

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
            _root.style.maxHeight = 400;

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
                return;

            // Nettoyer tous les labels informatifs (non-boutons)
            var childrenToRemove = new List<VisualElement>();
            foreach (var child in _scrollView.Children())
            {
                if (child is Label)
                {
                    childrenToRemove.Add(child);
                }
            }
            foreach (var child in childrenToRemove)
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
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(typeof(ScalarFieldGridSize));
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

            // Mettre à jour le header avec le compte
            if (_headerLabel != null)
            {
                _headerLabel.text = $"Scalar Fields ({entities.Length})";
            }

            // Supprimer les boutons des entités qui n'existent plus
            List<Entity> entitiesToRemove = new List<Entity>();
            foreach (var kvp in _entityButtons)
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
            foreach (var entity in entitiesToRemove)
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
                return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!entityManager.Exists(entity))
                return;

            // Basculer la sélection
            if (entityManager.HasComponent<ScalarFieldSelected>(entity))
            {
                entityManager.RemoveComponent<ScalarFieldSelected>(entity);
            }
            else
            {
                entityManager.AddComponent<ScalarFieldSelected>(entity);
            }

            // Mettre à jour l'affichage
            if (_entityButtons.TryGetValue(entity, out Button button))
            {
                UpdateButtonText(entity, button);
            }

            SceneView.RepaintAll();
        }

        private void UpdateButtonText(Entity entity, Button button)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null || button == null)
                return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!entityManager.Exists(entity))
                return;

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
    }
}
