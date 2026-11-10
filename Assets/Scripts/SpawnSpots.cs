using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    /// <summary>
    /// Deals enemies out over random spawn spots every time a level loads. Enemies placed close together along the level form
    /// a group around that point and share one pool of spots: their own placements plus random spots nearby, so that there are
    /// at least <c>spotsPerEnemy</c> spots per enemy. Every enemy keeps its type and mount. Each spot is on a flat surface
    /// (floor-side for standing enemies, ceiling-side for hangers) under the whole body, with room for the body and its hop,
    /// and clear of every other spot, so nothing floats or overlaps. The boss stays where its arena puts it.
    /// </summary>
    public static class SpawnSpots
    {
        /// <summary>Placements this close along the level, or chained together by gaps this short, share a pool of spots.</summary>
        const float GroupGap = 3f;
        /// <summary>No spot nearer the safe zone door than this, so nothing spawns on top of the player coming out of it.</summary>
        const float MinX = 6f;
        /// <summary>Gap kept between neighbouring bodies (and their hops).</summary>
        const float Spacing = 0.4f;
        /// <summary>Spacing of the positions along the level tried as random spots, in random order, until a group's pool is full.</summary>
        const float CandidateStep = 0.1f;
        /// <summary>
        /// The fit test keeps this far off the surface a body is mounted on, so resting on it doesn't count. Overlap queries pad
        /// every box by the physics engine's polygon radius, so this has to be well clear of that.
        /// </summary>
        const float Skin = 0.05f;

        static ContactFilter2D s_Solid = CreateSolidFilter();
        static readonly List<RaycastHit2D> s_Hits = new List<RaycastHit2D>(8);
        static readonly List<Collider2D> s_Overlaps = new List<Collider2D>(8);
        /// <summary>Footprints of every spot so far, in use or not.</summary>
        static readonly List<Rect> s_Taken = new List<Rect>();
        /// <summary>No spot further along the level than this: short of the end wall and its exit door, or of the boss arena.</summary>
        static float s_MaxX;

        static ContactFilter2D CreateSolidFilter()
        {
            var filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;
            return filter;
        }

        /// <summary>
        /// Picks a spawn spot for each enemy. Casts against the level's colliders, so call it once they're built and synced, and
        /// before the enemies are activated. Each enemy's mount, size and hop height must already be set.
        /// </summary>
        /// <param name="anchors">In: where the level places each enemy. Out: where it spawns (see <see cref="Enemy.PlaceOn"/>).</param>
        /// <param name="spotsPerEnemy">Spots in each group's pool per enemy in the group, its own placements included.</param>
        /// <param name="range">How far along the level a random spot can be from the group's placements; twice that where it's too cramped for enough spots.</param>
        /// <param name="maxX">No random spot further along the level than this (see <see cref="LevelData.SpawnMaxX"/>).</param>
        public static void Scatter(IReadOnlyList<Enemy> enemies, Vector2[] anchors, float spotsPerEnemy, float range, float maxX)
        {
            s_MaxX = maxX;
            // Every placement is off limits to random spots, the boss's included.
            s_Taken.Clear();
            var order = new List<int>(enemies.Count);
            for (int i = 0; i < enemies.Count; i++)
            {
                s_Taken.Add(Footprint(anchors[i], enemies[i].mount, enemies[i].size, enemies[i].hopHeight));
                if (!(enemies[i] is Boss))
                    order.Add(i);
            }
            order.Sort((a, b) => anchors[a].x.CompareTo(anchors[b].x));

            for (int start = 0; start < order.Count;)
            {
                int end = start + 1;
                while (end < order.Count && anchors[order[end]].x - anchors[order[end - 1]].x <= GroupGap)
                    end++;
                // Standing enemies and hangers need different surfaces, so each gets its own pool.
                var group = order.GetRange(start, end - start);
                ScatterGroup(group.FindAll(i => enemies[i].mount == EnemyMount.Floor), enemies, anchors, spotsPerEnemy, range);
                ScatterGroup(group.FindAll(i => enemies[i].mount == EnemyMount.Ceiling), enemies, anchors, spotsPerEnemy, range);
                start = end;
            }
        }

        /// <summary>Builds the pool for one group of same-mount enemies and deals it out.</summary>
        static void ScatterGroup(List<int> members, IReadOnlyList<Enemy> enemies, Vector2[] anchors, float spotsPerEnemy, float range)
        {
            if (members.Count == 0)
                return;

            EnemyMount mount = enemies[members[0]].mount;
            Vector2 size = Vector2.zero;
            float hop = 0f, minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            var spots = new List<Vector2>();
            foreach (int i in members)
            {
                size = Vector2.Max(size, enemies[i].size);
                hop = Mathf.Max(hop, enemies[i].hopHeight);
                minX = Mathf.Min(minX, anchors[i].x);
                maxX = Mathf.Max(maxX, anchors[i].x);
                spots.Add(anchors[i]);
            }
            int placed = spots.Count;

            // Surfaces are looked for from the room's floor (or ceiling, for hangers) and from each member's height, so ledges and
            // what's under them, angled plates included, all come up.
            var heights = new List<float> { mount == EnemyMount.Ceiling ? LevelData.Height : 0f };
            foreach (int i in members)
                if (!heights.Contains(anchors[i].y))
                    heights.Add(anchors[i].y);

            // Random spots fit the biggest member and the highest hop, so any member can take any of them. Where the range is too
            // cramped for enough of them, look twice as far before settling for fewer.
            int wanted = Mathf.CeilToInt(spotsPerEnemy * members.Count - 1e-4f);
            AddRandomSpots(spots, wanted, heights, minX - range, maxX + range, mount, size, hop);
            AddRandomSpots(spots, wanted, heights, minX - range * 2f, maxX + range * 2f, mount, size, hop);
            if (spots.Count < wanted)
                Debug.LogWarning($"Only {spots.Count} of {wanted} enemy spawn spots fit near x = {minX:0.#}–{maxX:0.#}.");

            // Deal the spots out at random, highest hops first: they have the fewest spots with room for them.
            members.Sort((a, b) => enemies[b].hopHeight.CompareTo(enemies[a].hopHeight));
            var free = new List<int>(spots.Count);
            for (int s = 0; s < spots.Count; s++)
                free.Add(s);
            var picks = new Vector2[members.Count];
            var options = new List<int>(spots.Count);
            for (int m = 0; m < members.Count; m++)
            {
                var enemy = enemies[members[m]];
                options.Clear();
                foreach (int s in free)
                    if (s >= placed || spots[s] == anchors[members[m]] || Fits(spots[s], enemy.mount, enemy.size, enemy.hopHeight))
                        options.Add(s);
                // Can't happen while every member fits its own placement; if it somehow does, the group spawns as placed.
                if (options.Count == 0)
                    return;
                int pick = options[Random.Range(0, options.Count)];
                free.Remove(pick);
                picks[m] = spots[pick];
            }
            for (int m = 0; m < members.Count; m++)
                anchors[members[m]] = picks[m];
        }

        /// <summary>
        /// Tries every position from <paramref name="lo"/> to <paramref name="hi"/> along the level, from each of
        /// <paramref name="heights"/>, in random order, adding spots until there are <paramref name="wanted"/>.
        /// </summary>
        static void AddRandomSpots(List<Vector2> spots, int wanted, List<float> heights, float lo, float hi, EnemyMount mount, Vector2 size, float hop)
        {
            if (spots.Count >= wanted)
                return;
            lo = Mathf.Max(MinX, lo);
            hi = Mathf.Min(s_MaxX, hi);

            var candidates = new List<Vector2>();
            float phase = Random.value * CandidateStep;
            foreach (float y in heights)
                for (float x = lo + phase; x <= hi; x += CandidateStep)
                    candidates.Add(new Vector2(x, y));
            for (int c = candidates.Count - 1; c > 0; c--)
            {
                int swap = Random.Range(0, c + 1);
                (candidates[c], candidates[swap]) = (candidates[swap], candidates[c]);
            }

            foreach (var candidate in candidates)
            {
                if (!TryFindSpot(candidate.x, candidate.y, mount, size, hop, out var spot))
                    continue;
                spots.Add(spot);
                s_Taken.Add(Footprint(spot, mount, size, hop));
                if (spots.Count >= wanted)
                    return;
            }
        }

        /// <summary>
        /// A spot at <paramref name="x"/> on the first surface a body's height past <paramref name="fromY"/> (above it for standing
        /// enemies, below it for hangers), if the body and its hop fit there and it's clear of every spot so far.
        /// </summary>
        static bool TryFindSpot(float x, float fromY, EnemyMount mount, Vector2 size, float hop, out Vector2 anchor)
        {
            anchor = default;
            Vector2 up = Up(mount);
            float edge = size.x * 0.5f - Skin;
            // Both sides of the body must land on the same flat surface, so it can't hang off a ledge or perch on a slope.
            if (!ProbeSurface(new Vector2(x - edge, fromY) + up * size.y, -up, out float left) ||
                !ProbeSurface(new Vector2(x + edge, fromY) + up * size.y, -up, out float right) ||
                Mathf.Abs(left - right) > 0.01f)
                return false;

            anchor = new Vector2(x, left);
            return Fits(anchor, mount, size, hop) && IsClear(Footprint(anchor, mount, size, hop));
        }

        /// <summary>Height of the first surface along <paramref name="dir"/> from <paramref name="origin"/>, if it faces straight back (a flat floor or ceiling).</summary>
        static bool ProbeSurface(Vector2 origin, Vector2 dir, out float y)
        {
            y = 0f;
            int count = Physics2D.Raycast(origin, dir, s_Solid, s_Hits, LevelData.Height + 2f);
            if (count == 0)
                return false;
            var nearest = s_Hits[0];
            for (int i = 1; i < count; i++)
                if (s_Hits[i].distance < nearest.distance)
                    nearest = s_Hits[i];
            y = nearest.point.y;
            // A cast that starts inside a block hits it straight away.
            return nearest.distance > 0f && Vector2.Dot(nearest.normal, -dir) > 0.99f;
        }

        /// <summary>Whether the body mounted on <paramref name="anchor"/>, and the space it hops through, are clear of level geometry.</summary>
        static bool Fits(Vector2 anchor, EnemyMount mount, Vector2 size, float hop)
        {
            float height = size.y + hop - Skin;
            Vector2 center = anchor + Up(mount) * (Skin + height * 0.5f);
            return Physics2D.OverlapBox(center, new Vector2(size.x, height), 0f, s_Solid, s_Overlaps) == 0;
        }

        static bool IsClear(Rect footprint)
        {
            var padded = new Rect(footprint.x - Spacing, footprint.y - Spacing, footprint.width + Spacing * 2f, footprint.height + Spacing * 2f);
            foreach (var taken in s_Taken)
                if (padded.Overlaps(taken))
                    return false;
            return true;
        }

        /// <summary>World-space box covering the body and its hop.</summary>
        static Rect Footprint(Vector2 anchor, EnemyMount mount, Vector2 size, float hop)
        {
            float height = size.y + hop;
            return new Rect(anchor.x - size.x * 0.5f, mount == EnemyMount.Ceiling ? anchor.y - height : anchor.y, size.x, height);
        }

        /// <summary>Away from the surface the enemy is mounted on.</summary>
        static Vector2 Up(EnemyMount mount) => mount == EnemyMount.Ceiling ? Vector2.down : Vector2.up;
    }
}
