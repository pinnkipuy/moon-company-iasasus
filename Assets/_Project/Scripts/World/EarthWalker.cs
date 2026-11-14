// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Біг інженера на ЗЕМЛІ (наприклад, у зоні запуску). Як EngineerController, але БЕЗ місячних стрибків:
/// на Землі гравітація звичайна, тож персонаж просто біжить, кадри ходьби йдуть по колу 1→2→3→4.
/// Немає ні підстрибування спрайта, ні тіні-стрибка, ні пилу, ні ковзання.
///
/// Два набори картинок: у скафандрі (SuitOn = true) і без нього (SuitOn = false).
/// Перемикати можна з коду: GetComponent&lt;EarthWalker&gt;().SuitOn = false;
///
/// Як налаштувати:
///  1) Постав цей компонент на об'єкт персонажа (на ньому має бути CharacterController — Unity додасть сам).
///  2) Меню Moon → Налаштувати бігуна на Землі (спрайти) — заповнить картинки і створить дочірній Sprite.
///  3) Збережи сцену (Ctrl+S).
/// CharacterController — вбудований компонент Unity, який рухає персонажа і не дає пройти крізь стіни.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EarthWalker : MonoBehaviour
{
    [Header("Рух")]
    [Tooltip("Швидкість бігу, метрів за секунду")]
    [SerializeField] float speed = 5f;
    [Tooltip("Скільки кадрів анімації бігу за секунду")]
    [SerializeField] float framesPerSecond = 10f;
    [Tooltip("Межа карти: персонаж не відійде від центру далі, ніж на стільки метрів по X і Z")]
    [SerializeField] float mapHalfSize = 100f;
    [Tooltip("Чи слухатися клавіш WASD/стрілок. Вимкни, якщо рухатимеш персонажа сам через Move()")]
    [SerializeField] bool useKeyboard = true;

    [Header("Спрайт")]
    [SerializeField] SpriteRenderer spriteRenderer;
    [Tooltip("У скафандрі")]
    public EngineerController.SpriteSet suit = new EngineerController.SpriteSet();
    [Tooltip("Без скафандра")]
    public EngineerController.SpriteSet casual = new EngineerController.SpriteSet();
    [SerializeField] bool suitOn = false;

    CharacterController controller;
    Transform cam;
    Vector2 facing = new Vector2(0f, -1f); // куди дивиться «на екрані»: спочатку обличчям до камери
    Vector2 scriptInput;                   // напрямок від коду (Move), якщо клавіші вимкнені
    float frameTime;                       // скільки часу йде анімація бігу

    /// <summary>Чи персонаж зараз біжить.</summary>
    public bool IsMoving { get; private set; }

    /// <summary>true = персонаж не слухається клавіш (наприклад, поки йде заставка).</summary>
    public bool Locked { get; set; }

    /// <summary>У скафандрі чи ні.</summary>
    public bool SuitOn
    {
        get => suitOn;
        set { suitOn = value; RefreshSprite(); }
    }

    /// <summary>Рух з коду (замість клавіш): x — вправо по екрану, y — вгору по екрану. Vector2.zero = стоп.</summary>
    public void Move(Vector2 screenDir) => scriptInput = Vector2.ClampMagnitude(screenDir, 1f);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (CharacterSprites.IsEmpty(suit) && CharacterSprites.IsEmpty(casual))
            Debug.LogWarning("EarthWalker: немає картинок. Запусти меню Moon → Налаштувати бігуна на Землі (спрайти) і збережи сцену.");
        else
        {
            var cube = GetComponent<MeshRenderer>();
            if (cube != null) cube.enabled = false; // є спрайт — кубик-заглушка не потрібен
        }
        RefreshSprite();
    }

    void Update()
    {
        bool paused = IsGamePaused();
        Vector2 input = Locked || paused ? Vector2.zero : (useKeyboard ? ReadInput() : scriptInput);
        IsMoving = input.sqrMagnitude > 0.01f;

        if (IsMoving)
        {
            // На Землі зчеплення добре: стартує і зупиняється одразу, без ковзання
            controller.Move(ScreenToGround(input) * speed * Time.deltaTime);
            ClampToMap();
            facing = input;
            frameTime += Time.deltaTime;
        }
        else frameTime = 0f;

        RefreshSprite();
    }

    /// <summary>Напрямок «на екрані» → напрямок на землі з урахуванням повороту камери.</summary>
    Vector3 ScreenToGround(Vector2 input)
    {
        Vector3 forward = Vector3.forward, right = Vector3.right;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam != null)
        {
            forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        }
        Vector3 dir = forward * input.y + right * input.x;
        dir.y = 0f;
        return dir;
    }

    static Vector2 ReadInput()
    {
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        Vector2 v = Vector2.zero;
        if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
        if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
        return v.sqrMagnitude > 1f ? v.normalized : v; // по діагоналі не швидше
    }

    static bool IsGamePaused()
    {
        var game = GameBootstrap.Instance; // на сцені Землі може не бути Game — тоді паузи немає
        return game != null && game.Clock != null && game.Clock.IsPaused;
    }

    void ClampToMap()
    {
        Vector3 p = transform.position;
        float x = Mathf.Clamp(p.x, -mapHalfSize, mapHalfSize);
        float z = Mathf.Clamp(p.z, -mapHalfSize, mapHalfSize);
        if (x != p.x || z != p.z)
        {
            controller.enabled = false; // CharacterController не дає просто так міняти позицію
            transform.position = new Vector3(x, p.y, z);
            controller.enabled = true;
        }
    }

    // ---------- Картинка ----------

    /// <summary>Вибирає кадр: напрямок, стоїть чи біжить, у скафандрі чи ні.</summary>
    void RefreshSprite()
    {
        if (spriteRenderer == null) return;

        var set = suitOn ? suit : casual;
        if (CharacterSprites.IsEmpty(set)) set = suitOn ? casual : suit; // одного набору нема — беремо інший

        bool flip = false;
        Sprite[] walk;
        Sprite idle;
        if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
        {
            bool right = facing.x > 0f;
            walk = right ? set.walkRight : set.walkLeft;
            if (right && CharacterSprites.IsEmpty(walk)) { walk = set.walkLeft; flip = true; } // правих кадрів нема — дзеркалимо ліві
            idle = CharacterSprites.IsEmpty(walk) ? set.idleFront : walk[0];
        }
        else if (facing.y > 0f) { walk = set.walkBack; idle = set.idleBack; }
        else { walk = set.walkFront; idle = set.idleFront; }

        Sprite frame = idle;
        if (IsMoving && !CharacterSprites.IsEmpty(walk))
            frame = walk[(int)(frameTime * framesPerSecond) % walk.Length]; // кадри по колу

        if (frame != null) spriteRenderer.sprite = frame;
        spriteRenderer.flipX = flip;
    }
}
