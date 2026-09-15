using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Logs every renderer's material/shader to identify magenta culprits.</summary>
public class RenderDiagnostics
{
    [UnityTest]
    public IEnumerator PlayersStayGrounded()
    {
        for (int n = 1; n <= SceneRouter.LevelCount; n++)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(SceneRouter.LevelSceneName(n));
            while (!op.isDone)
                yield return null;

            var p1 = IndieMarc.Platformer.PlayerCharacterOne.Get(1);
            var p2 = IndieMarc.Platformer.PlayerCharacterTwo.Get(2);
            float y1Start = p1.transform.position.y;
            float y2Start = p2.transform.position.y;

            // Collision introspection: what physics exists under P1?
            RaycastHit2D hit = Physics2D.Raycast(
                p1.transform.position, Vector2.down, 30f, ~0);
            Debug.Log($"[RD] L{n} raycast-down from {p1.transform.position}: " +
                      (hit.collider != null
                        ? $"hit {hit.collider.gameObject.name} (layer {hit.collider.gameObject.layer}) at y={hit.point.y:F1}"
                        : "NO HIT — no ground physics below spawn"));
            foreach (var tc in Object.FindObjectsOfType<UnityEngine.Tilemaps.TilemapCollider2D>())
            {
                var composite = tc.GetComponent<CompositeCollider2D>();
                Debug.Log($"[RD] L{n} tilemapCollider on {tc.gameObject.name}: layer={tc.gameObject.layer} " +
                          $"usedByComposite={tc.usedByComposite} shapes={tc.shapeCount} " +
                          $"compositePaths={(composite != null ? composite.pathCount : -1)}");
            }
            Debug.Log($"[RD] L{n} P1 layer={p1.gameObject.layer} " +
                      $"ignore(P1,Platform)={Physics2D.GetIgnoreLayerCollision(p1.gameObject.layer, LayerMask.NameToLayer("Platform"))}");

            float t = 0f;
            while (t < 2f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            // Relative checks, robust to any scene's world origin.
            Debug.Log($"[RD] L{n} after 2s: P1 y {y1Start:F1}→{p1.transform.position.y:F1}, " +
                      $"P2 y {y2Start:F1}→{p2.transform.position.y:F1}");
            Assert.Greater(p1.transform.position.y, y1Start - 5f,
                $"L{n}: Player1 fell through the world");
            Assert.Greater(p2.transform.position.y, y2Start - 5f,
                $"L{n}: Player2 fell through the world");
        }
    }

    [UnityTest]
    public IEnumerator DumpRenderersForL1AndL5()
    {
        foreach (int n in new[] { 1, 5 })
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(SceneRouter.LevelSceneName(n));
            while (!op.isDone)
                yield return null;
            yield return null;

            Debug.Log($"=== RENDERERS L{n} ===");
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
            {
                Material m = r.sharedMaterial;
                string shader = m != null && m.shader != null ? m.shader.name : "NULL";
                bool supported = m != null && m.shader != null && m.shader.isSupported;
                string sprite = r is SpriteRenderer sr && sr.sprite != null ? sr.sprite.name : "-";
                Debug.Log($"[RD] {r.gameObject.name} | {r.GetType().Name} | mat={(m != null ? m.name : "NULL")} | shader={shader} | supported={supported} | sprite={sprite} | pos={r.transform.position}");
            }
        }
        Assert.Pass();
    }
}
