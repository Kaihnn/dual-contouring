# Instructions Copilot

## Contexte du projet
- Framework : Unity avec ECS (Entity Component System)
- Langage : C#
- Architecture : Unity Entities (DOTS)

## Règles de codage

### Conventions de nommage C#
- PascalCase pour les classes, méthodes, propriétés, interfaces
- camelCase pour les variables locales et paramètres
- PascalCase pour les champs publics
- camelCase pour les champs privés (sans préfixe)
- Pas de notation hongroise

### Commentaires
- Éviter les commentaires évidents ou redondants
- Commenter uniquement la logique complexe ou non-évidente
- Pas de commentaires pour du code auto-explicite
- Privilégier des noms de variables et méthodes clairs plutôt que des commentaires

### Bonnes pratiques Unity Entities
- Utiliser `[BurstCompile]` pour les systèmes compatibles
- Préférer `RefRO<T>` et `RefRW<T>` pour l'accès aux composants
- Utiliser `DynamicBuffer<T>` pour les collections
- Utiliser `SystemAPI.Query` pour les requêtes ECS
- Types de données : préférer `float3`, `int3`, `quaternion` de Unity.Mathematics
- Utiliser `partial struct` pour les systèmes implémentant `ISystem`

### Style général
- Code concis et lisible
- Noms de variables et méthodes explicites
- Éviter les commentaires de type "cette méthode fait X" quand le nom l'indique déjà
- Préférer l'immutabilité quand c'est possible

