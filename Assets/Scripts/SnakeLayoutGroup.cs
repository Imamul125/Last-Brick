using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Layout/Snake Layout Group")]
public class SnakeLayoutGroup : LayoutGroup
{
    public enum StartCorner { Top, Bottom }

    [Header("Snake Layout Settings")]
    [Tooltip("Where the first element should be placed.")]
    public StartCorner startCorner = StartCorner.Bottom;

    [Tooltip("The fixed width and height of each badge.")]
    public Vector2 cellSize = new Vector2(150f, 150f);

    [Tooltip("Vertical distance between each badge.")]
    public float verticalSpacing = 150f;
    
    [Tooltip("How far left and right the snake swings from the center.")]
    public float amplitude = 200f;
    
    [Tooltip("How fast the snake curves. Higher values = tighter coils.")]
    public float frequency = 0.5f;

    [Tooltip("Add an offset to shift the starting point of the sine wave.")]
    public float phaseOffset = 0f;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        // The total horizontal space needed is the padding plus twice the amplitude (left and right swings)
        float minWidth = padding.horizontal + (amplitude * 2f);
        SetLayoutInputForAxis(minWidth, minWidth, -1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        float totalHeight = padding.vertical;
        
        int activeChildren = 0;
        float lastChildHeight = 0f;

        for (int i = 0; i < rectChildren.Count; i++)
        {
            if (rectChildren[i].gameObject.activeInHierarchy)
            {
                activeChildren++;
                lastChildHeight = cellSize.y;
            }
        }
        
        if (activeChildren > 0)
        {
            // Height is total spacing gaps + the height of the single largest/last item
            totalHeight += (activeChildren - 1) * verticalSpacing;
            totalHeight += lastChildHeight;
        }

        SetLayoutInputForAxis(totalHeight, totalHeight, -1, 1);
    }

    public override void SetLayoutHorizontal()
    {
        int activeIndex = 0;
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            if (!child.gameObject.activeInHierarchy) continue;

            // Calculate Sine Wave X offset
            float sineValue = Mathf.Sin((activeIndex * frequency) + phaseOffset);
            float xPos = sineValue * amplitude;
            
            // Center the item inside this container's available width
            float availableWidth = rectTransform.rect.width;
            float centerOffset = padding.left + ((availableWidth - padding.horizontal) * 0.5f);
            
            float finalX = centerOffset + xPos - (cellSize.x * 0.5f);

            SetChildAlongAxis(child, 0, finalX, cellSize.x);
            activeIndex++;
        }
    }

    public override void SetLayoutVertical()
    {
        int activeIndex = 0;
        int totalActive = 0;
        
        for (int i = 0; i < rectChildren.Count; i++)
        {
            if (rectChildren[i].gameObject.activeInHierarchy) totalActive++;
        }

        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            if (!child.gameObject.activeInHierarchy) continue;

            float yPos;
            if (startCorner == StartCorner.Top)
            {
                yPos = padding.top + (activeIndex * verticalSpacing);
            }
            else
            {
                // Start from the bottom: SetChildAlongAxis(1) sets the distance from the TOP edge.
                // So yPos = containerHeight - padding.bottom - childHeight - (index * spacing)
                float totalHeight = rectTransform.rect.height;
                yPos = totalHeight - padding.bottom - cellSize.y - (activeIndex * verticalSpacing);
            }

            SetChildAlongAxis(child, 1, yPos, cellSize.y);
            activeIndex++;
        }
    }
}
