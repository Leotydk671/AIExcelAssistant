using Godot;
using System;

[Tool]
[GlobalClass]
public partial class StyleBoxLabel : StyleBox // 关键：继承 StyleBox，而不是 StyleBoxFlat
{
    // ========== 可调节属性 ==========
    [Export(PropertyHint.Range, "0,100")]
    public float CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Mathf.Max(0, value);
            EmitChanged();
        }
    }
    private float _cornerRadius = 12.0f;

    [Export(PropertyHint.Range, "0,50")]
    public float TopIndentDepth
    {
        get => _topIndentDepth;
        set
        {
            _topIndentDepth = Mathf.Clamp(value, 0, 50);
            EmitChanged();
        }
    }
    private float _topIndentDepth = 15.0f;

    [Export]
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            EmitChanged();
        }
    }
    private Color _backgroundColor = new Color(0.96f, 0.93f, 0.84f); // 米色

    [Export(PropertyHint.Range, "4,32")]
    public int CornerSegments
    {
        get => _cornerSegments;
        set
        {
            _cornerSegments = Mathf.Clamp(value, 4, 32);
            EmitChanged();
        }
    }
    private int _cornerSegments = 8; // 每个圆角用多少线段近似

    // ========== 核心方法 ==========
    /*public override Rid GetDrawCacheKey()
    {
        // 返回唯一标识，启用自定义绘制
        return RenderingServer.GetRid(this);
    }*/

    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        // 计算并绘制多边形
        Vector2[] vertices = CalculateTabVertices(rect);
        RenderingServer.CanvasItemAddPolygon(canvasItem, vertices, [BackgroundColor]);

        // 可选：绘制边框线用于调试
        // DrawDebugOutline(canvasItem, vertices);
    }

    /*public override Vector2 GetMinimumSize()
    {
        return new Vector2(CornerRadius * 4, TopIndentDepth + CornerRadius * 2);
    }*/

    // ========== 顶点计算核心算法 ==========
    private Vector2[] CalculateTabVertices(Rect2 rect)
    {
        // 计算关键坐标点
        float left = rect.Position.X;
        float right = rect.End.X;
        float top = rect.Position.Y;
        float bottom = rect.End.Y;
        float centerX = rect.GetCenter().X;

        // 顶部凹陷的Y坐标
        //float topIndentY = top + TopIndentDepth;

        // 顶点列表（按顺时针顺序）
        var vertices = new System.Collections.Generic.List<Vector2>();

        // 1. 左上角区域（内凹圆角）
        // 从顶部凹陷的左侧开始
        vertices.Add(new Vector2(left + CornerRadius, top));
        
        // 左上内凹圆角（二次贝塞尔曲线模拟）
        AddOutsetCorner(vertices,
						center: new Vector2(left + CornerRadius, top - CornerRadius),
						startAngle:90,
						endAngle:180,
						radius:CornerRadius,
						segments: CornerSegments);

        // 2. 左下角（外凸圆角）
        /*AddOutsetCorner(vertices,
            center: new Vector2(left + CornerRadius, bottom - CornerRadius),
            startAngle: Mathf.Pi, // 180度开始
            endAngle: Mathf.Pi * 1.5f, // 270度结束
            radius: CornerRadius,
            segments: CornerSegments / 2);

        // 3. 右下角（外凸圆角）
        AddOutsetCorner(vertices,
            center: new Vector2(right - CornerRadius, bottom - CornerRadius),
            startAngle: Mathf.Pi * 1.5f, // 270度开始
            endAngle: Mathf.Pi * 2f, // 360度结束
            radius: CornerRadius,
            segments: CornerSegments / 2); */

        // 4. 右上角区域（内凹圆角）
        // 先到右上角起点
        vertices.Add(new Vector2(right, top + CornerRadius));
        
        // 右上内凹圆角
        AddInsetCorner(vertices,
            start: new Vector2(right, top + CornerRadius),
            control: new Vector2(right, top),
            end: new Vector2(right - CornerRadius, top),
            segments: CornerSegments / 2,
            isRightSide: true);

        return vertices.ToArray();
    }

    // 添加内凹圆角顶点（使用二次贝塞尔曲线）
    private void AddInsetCorner(System.Collections.Generic.List<Vector2> vertices,
        Vector2 start, Vector2 control, Vector2 end, int segments, bool isRightSide)
    {
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            // 二次贝塞尔曲线公式
            Vector2 point = (1 - t) * (1 - t) * start 
                          + 2 * (1 - t) * t * control 
                          + t * t * end;
            vertices.Add(point);
        }
    }

    // 添加外凸圆角顶点（使用圆弧）
    private void AddOutsetCorner(System.Collections.Generic.List<Vector2> vertices,
        Vector2 center, float startAngle, float endAngle, float radius, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = startAngle + (endAngle - startAngle) * t;
            Vector2 point = new Vector2(
                center.X + Mathf.Cos(angle) * radius,
                center.Y + Mathf.Sin(angle) * radius
            );
            vertices.Add(point);
        }
    }




    // ========== 调试辅助方法 ==========
    private void DrawDebugOutline(Rid canvasItem, Vector2[] vertices)
    {
        // 绘制顶点连线（红色）
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 from = vertices[i];
            Vector2 to = vertices[(i + 1) % vertices.Length];
            RenderingServer.CanvasItemAddLine(canvasItem, from, to, Colors.Red, 1.0f);
        }

        // 绘制顶点位置（绿色小圆）
        foreach (Vector2 vertex in vertices)
        {
            DrawCircle(canvasItem, vertex, 2.0f, Colors.Green);
        }
    }

    private void DrawCircle(Rid canvasItem, Vector2 center, float radius, Color color)
    {
        const int segments = 12;
        var points = new Vector2[segments];
        
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (Mathf.Pi * 2 / segments);
            points[i] = new Vector2(
                center.X + Mathf.Cos(angle) * radius,
                center.Y + Mathf.Sin(angle) * radius
            );
        }
        
        RenderingServer.CanvasItemAddPolygon(canvasItem, points, new Color[] { color });
    }
}