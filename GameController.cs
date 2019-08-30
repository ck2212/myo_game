using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

using LockingPolicy = Thalmic.Myo.LockingPolicy;
using Pose = Thalmic.Myo.Pose;
using UnlockType = Thalmic.Myo.UnlockType;
using VibrationType = Thalmic.Myo.VibrationType;

public class GameController : MonoBehaviour
{
    public Camera gameCamera;
    public GameObject bulletPrefab;
    public GameObject enemyPrefab;

    public float enemySpawingCooldown = 1f;
    public float enemySpawningDistance = 7f;
    public float shootingCooldown = 0.5f;

    private float enemySpawningTimer = 0;
    private float shootingTimer = 0;

    public GameObject Enemies;

    /// <summary>
    /// ///////////////////////////////////////////
    /// </summary>
    public GameObject myo = null;
    private Quaternion _antiYaw = Quaternion.identity;
    private float _referenceRoll = 0.0f;
    private Pose _lastPose = Pose.Unknown;

    public float Timer;
    // Start is called before the first frame update
    void Start()
    {
       
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.tag=="Enemy")
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
              
    }

    // Update is called once per frame
    void Update()
    {
        Timer += Time.deltaTime;

        /*if(Timer > 5.0f)
        {
            SceneChange(1);
        }*/
        shootingTimer -= Time.deltaTime;
        enemySpawningTimer -= Time.deltaTime;

        if (enemySpawningTimer <= 0f)
        {
            enemySpawningTimer = enemySpawingCooldown;

            GameObject enemyObject = Instantiate(enemyPrefab);
            enemyObject.transform.SetParent(Enemies.transform);
            float randomAngle = Random.Range(0, Mathf.PI);

            enemyObject.transform.position = new Vector3(
              gameCamera.transform.position.x + Mathf.Cos(randomAngle) * enemySpawningDistance,
              gameCamera.transform.position.y,
              gameCamera.transform.position.z + Mathf.Sin(randomAngle) * enemySpawningDistance
                );
            Enemy enemy = enemyObject.GetComponent<Enemy>();
            enemy.direction = (gameCamera.transform.position - enemy.transform.position).normalized;
        }

        RaycastHit hit;
        ThalmicMyo DoPose = myo.GetComponent<ThalmicMyo>();
       
        if (Physics.Raycast(gameCamera.transform.position, gameCamera.transform.forward, out hit))
        {
            if(hit.transform.tag=="Enemy" && shootingTimer<=0f)
            {

                if (DoPose.pose == Pose.FingersSpread)
                {
                    shootingTimer = shootingCooldown;
                    GameObject bulletObject = Instantiate(bulletPrefab);
                    bulletObject.transform.position = gameCamera.transform.position;

                    Bullet bullet = bulletObject.GetComponent<Bullet>();
                    bullet.direction = gameCamera.transform.forward;
                }

                /*else if (DoPose.pose != Pose.FingersSpread)
                {

                }*/
                                
            }

            
        }

        ///////////////////////////
        ///
        // Access the ThalmicMyo component attached to the Myo object.
        ThalmicMyo thalmicMyo = myo.GetComponent<ThalmicMyo>();

        // Update references when the pose becomes fingers spread or the q key is pressed.
        bool updateReference = false;
        if (thalmicMyo.pose != _lastPose)
        {
            _lastPose = thalmicMyo.pose;

            if (thalmicMyo.pose == Pose.DoubleTap)
            {
                updateReference = true;

                ExtendUnlockAndNotifyUserAction(thalmicMyo);
            }
        }
        if (Input.GetKeyDown("r"))
        {
            updateReference = true;
        }

        // Update references. This anchors the joint on-screen such that it faces forward away
        // from the viewer when the Myo armband is oriented the way it is when these references are taken.
        if (updateReference)
        {
            // _antiYaw represents a rotation of the Myo armband about the Y axis (up) which aligns the forward
            // vector of the rotation with Z = 1 when the wearer's arm is pointing in the reference direction.
            _antiYaw = Quaternion.FromToRotation(
                new Vector3(myo.transform.forward.x, 0, myo.transform.forward.z),
                new Vector3(0, 0, 1)
            );
        }

        
        

        // Here the anti-roll and yaw rotations are applied to the myo Armband's forward direction to yield
        // the orientation of the joint.
        transform.rotation = _antiYaw * Quaternion.LookRotation(myo.transform.forward);

        // The above calculations were done assuming the Myo armbands's +x direction, in its own coordinate system,
        // was facing toward the wearer's elbow. If the Myo armband is worn with its +x direction facing the other way,
        // the rotation needs to be updated to compensate.
        if (thalmicMyo.xDirection == Thalmic.Myo.XDirection.TowardWrist)
        {
            // Mirror the rotation around the XZ plane in Unity's coordinate system (XY plane in Myo's coordinate
            // system). This makes the rotation reflect the arm's orientation, rather than that of the Myo armband.
            transform.rotation = new Quaternion(transform.localRotation.x,
                                                -transform.localRotation.y,
                                                transform.localRotation.z,
                                                -transform.localRotation.w);
        }
    }

    // Compute the angle of rotation clockwise about the forward axis relative to the provided zero roll direction.
    // As the armband is rotated about the forward axis this value will change, regardless of which way the
    // forward vector of the Myo is pointing. The returned value will be between -180 and 180 degrees.
    float rollFromZero(Vector3 zeroRoll, Vector3 forward, Vector3 up)
    {
        // The cosine of the angle between the up vector and the zero roll vector. Since both are
        // orthogonal to the forward vector, this tells us how far the Myo has been turned around the
        // forward axis relative to the zero roll vector, but we need to determine separately whether the
        // Myo has been rolled clockwise or counterclockwise.
        float cosine = Vector3.Dot(up, zeroRoll);

        // To determine the sign of the roll, we take the cross product of the up vector and the zero
        // roll vector. This cross product will either be the same or opposite direction as the forward
        // vector depending on whether up is clockwise or counter-clockwise from zero roll.
        // Thus the sign of the dot product of forward and it yields the sign of our roll value.
        Vector3 cp = Vector3.Cross(up, zeroRoll);
        float directionCosine = Vector3.Dot(forward, cp);
        float sign = directionCosine < 0.0f ? 1.0f : -1.0f;

        // Return the angle of roll (in degrees) from the cosine and the sign.
        return sign * Mathf.Rad2Deg * Mathf.Acos(cosine);
    }

    // Compute a vector that points perpendicular to the forward direction,
    // minimizing angular distance from world up (positive Y axis).
    // This represents the direction of no rotation about its forward axis.
    Vector3 computeZeroRollVector(Vector3 forward)
    {
        Vector3 antigravity = Vector3.up;
        Vector3 m = Vector3.Cross(myo.transform.forward, antigravity);
        Vector3 roll = Vector3.Cross(m, myo.transform.forward);

        return roll.normalized;
    }

    // Adjust the provided angle to be within a -180 to 180.
    float normalizeAngle(float randomAngle)
    {
        if (randomAngle > 180.0f)
        {
            return randomAngle - 360.0f;
        }
        if (randomAngle < -180.0f)
        {
            return randomAngle + 360.0f;
        }
        return randomAngle;
    }

    // Extend the unlock if ThalmcHub's locking policy is standard, and notifies the given myo that a user action was
    // recognized.
    void ExtendUnlockAndNotifyUserAction(ThalmicMyo myo)
    {
        ThalmicHub hub = ThalmicHub.instance;

        if (hub.lockingPolicy == LockingPolicy.Standard)
        {
            myo.Unlock(UnlockType.Timed);
        }

        myo.NotifyUserAction();
    }

    void SceneChange(int SceneNo)
    {
        SceneManager.LoadSceneAsync(SceneNo);
    }
}