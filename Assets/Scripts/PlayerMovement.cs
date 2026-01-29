using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Coyote Time")]
    [SerializeField] private float coyoteTime = 0.2f;

    private Rigidbody2D _rb;
    private bool _isGrounded;
    private float _moveInput;
    private float _coyoteTimeCounter;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Coyote Time Counter
        if (_isGrounded)
        {
            _coyoteTimeCounter = coyoteTime;
        }
        else
        {
            _coyoteTimeCounter -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        // Ground Check
        _isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Movement
        _rb.linearVelocity = new Vector2(_moveInput * moveSpeed, _rb.linearVelocity.y);
    }

    // Called by PlayerInput component
    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>().x;
    }

    // Called by PlayerInput component
    public void OnJump(InputValue value)
    {
        if (value.isPressed && _coyoteTimeCounter > 0f)
        {
            Jump();
        }
    }

    private void Jump()
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
        _coyoteTimeCounter = 0f;
    }

    // Public method for respawning (can be called from other scripts if needed)
    public void Respawn(Vector3 position)
    {
        transform.position = position;
        _rb.linearVelocity = Vector2.zero;
        _coyoteTimeCounter = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}