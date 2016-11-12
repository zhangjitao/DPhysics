﻿using UnityEngine;
using System.Collections.Generic;
using FixedPointMath;

/// <summary>
/// Class defining the physics engine core.
/// </summary>
public class PhysicsEngine : MonoBehaviour{

    public const int maxObjectCount = 4096;
    public int sceneWidth;
    public int sceneHeight;
    public int cellSize;
    public bool draw;

    private static PhysicsEngine instance;
    private static int bodyCount;

    private List<PhysicsObject> objects;
    private List<KeyValuePair<DCollider, DCollider>> collisions;

    private CollisionDetector detector;
    private bool simulate;

    //TODO: remove this
    private GUIStyle style;

    /// <summary>
    /// Initializes the engine and sets the instance value.
    /// </summary>
    void Awake() {
        instance = this;
        bodyCount = 0;
        objects = new List<PhysicsObject>();
        collisions = new List<KeyValuePair<DCollider, DCollider>>();
        detector = new HashGridDetector(cellSize, sceneWidth, sceneHeight);
        simulate = false;
    }

    /// <summary>
    /// Starts the simulation (temporary).
    /// </summary>
    void Start() {
        simulate = true;
        style = new GUIStyle();
        style.normal.textColor = Color.white;
    }

    /// <summary>
    /// Calculates the current number of frames and updates the physics.
    /// </summary>
    void Update() {
        if (simulate) {
            int frames = (int)(1 / Time.deltaTime);
            UpdatePhysics(frames);
        }
    }

    void OnGUI() {
        Rect label = new Rect(100, 100, 300, 20);
        GUIStyle style = new GUIStyle();
        GUI.Label(label, "body count: " + bodyCount, style);
    }

    /// <summary>
    /// Draw the quadtree.
    /// </summary>
    void OnDrawGizmos() {
        Vector3 center = new Vector3(sceneWidth / 2, 0, sceneHeight / 2);
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(center, new Vector3(sceneWidth, 1, sceneHeight));
        if (detector == null || !draw)
            return;
        detector.Draw();
    }

    /// <summary>
    /// Returns the singleton instance for the engine.
    /// </summary>
    public static PhysicsEngine Instance {
        get { return instance; }
    }

    /// <summary>
    /// Adds a physics object to the physics environment.
    /// </summary>
    /// <param name="obj">the new object.</param>
    public void AddObject(PhysicsObject obj) {
        if (bodyCount > maxObjectCount)
            return;

        obj.SetID(bodyCount);
        bodyCount++;
        this.objects.Add(obj);
        this.detector.Insert(obj);
    }

    /// <summary>
    /// Main physics loop, find collisions, resolve them and move the bodies.
    /// </summary>
    /// <param name="delta"> amount of time for this simulation step</param>
    private void UpdatePhysics(int frames) {

        Profiler.BeginSample("Collision detection");
        HashSet<Intersection> broadPhaseCollisions = detector.FindPotentialCollisions();
        Profiler.EndSample();
        foreach (Intersection collision in broadPhaseCollisions) {
            if (!collision.IsTrigger()) {
                ResolveCollision(collision);
            }
        }
    
        //for each physics object, apply forces
        foreach(PhysicsObject obj in objects) {
            if (!obj.IsFixed() && obj.Velocity != Vector2f.Zero) {

                Profiler.BeginSample("Remove");
                detector.Remove(obj);
                Profiler.EndSample();

                obj.Integrate(frames);

                Profiler.BeginSample("Insert");
                detector.Insert(obj);
                Profiler.EndSample();
            }
        }
    }

    /// <summary>
    /// Resolve the collision by calculating the resulting velocity, using the
    /// given normal and penetration, stored in the intersection.
    /// </summary>
    /// <param name="collision">Intersection instance containing all the collision data.</param>
    private void ResolveCollision(Intersection collision) {
        PhysicsObject a = collision.GetA();
        PhysicsObject b = collision.GetB();
        Vector2f rv = b.Velocity - a.Velocity;
        intf nvel = Vector2f.Dot(rv, collision.Normal);
        if (nvel > 0)
            return;

        intf e = FixedMath.Min(a.Restitution, b.Restitution);
        intf j = (-(1 + e) * nvel) / (a.InvMass + b.InvMass);

        Vector2f impulse = collision.Normal * j;
        a.Velocity -= impulse * a.InvMass;
        b.Velocity += impulse * b.InvMass;
    }
}
