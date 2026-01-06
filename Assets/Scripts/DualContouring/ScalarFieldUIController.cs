using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace DualContouring
{
    /// <summary>
    ///     Contrôleur pour l'interface utilisateur du champ scalaire
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ScalarFieldUIController : MonoBehaviour
    {
        private readonly Dictionary<Entity, Button> _entityButtons = new Dictionary<Entity, Button>();
        private VisualElement _root;
        private ScrollView _scrollView;
        private UIDocument _uiDocument;

        private void Update()
        {
            // Rafraîchir la liste si nécessaire (peut être optimisé avec un système d'événements)
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                RefreshEntityButtons();
            }
        }

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;

            // Initialiser les éléments UI ici
            InitializeUI();
        }

        private void OnDisable()
        {
            // Nettoyer
            _entityButtons.Clear();
            if (_scrollView != null)
            {
                _scrollView.Clear();
            }
        }

        private void InitializeUI()
        {
            // Trouver la ScrollView
            _scrollView = _root.Q<ScrollView>("ScalarFieldList");
            if (_scrollView == null)
            {
                return;
            }

            // Nettoyer le contenu existant
            _scrollView.Clear();

            // Créer les boutons pour chaque entité
            RefreshEntityButtons();
        }

        private void RefreshEntityButtons()
        {
            if (World.DefaultGameObjectInjectionWorld == null || _scrollView == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(typeof(ScalarFieldGridSize));
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

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
                    // Mettre à jour le texte du bouton existant
                    UpdateButtonText(entity, _entityButtons[entity]);
                }
            }

            entities.Dispose();
        }

        private void CreateButtonForEntity(Entity entity, int index)
        {
            var button = new Button();
            button.name = $"ScalarFieldButton_{index}";
            UpdateButtonText(entity, button);

            button.clicked += () => OnEntityButtonClicked(entity);

            _scrollView.Add(button);
            _entityButtons[entity] = button;
        }

        private void OnEntityButtonClicked(Entity entity)
        {
            if (World.DefaultGameObjectInjectionWorld == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!entityManager.Exists(entity))
            {
                return;
            }

            // Basculer la sélection
            if (entityManager.HasComponent<ScalarFieldSelected>(entity))
            {
                entityManager.RemoveComponent<ScalarFieldSelected>(entity);
            }
            else
            {
                entityManager.AddComponent<ScalarFieldSelected>(entity);
            }

            // Mettre à jour le texte du bouton
            if (_entityButtons.TryGetValue(entity, out Button button))
            {
                UpdateButtonText(entity, button);
            }
        }

        private void UpdateButtonText(Entity entity, Button button)
        {
            if (World.DefaultGameObjectInjectionWorld == null || button == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (!entityManager.Exists(entity))
            {
                return;
            }

            button.text = $"{entity.Index}";
        }
    }
}