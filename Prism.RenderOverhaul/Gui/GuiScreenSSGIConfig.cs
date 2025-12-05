using Prism.Render.Config;
using Prism.Render.Gui.Controls;
using Sandbox;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Utils;
using VRageMath;

namespace Prism.Render.Gui;

public abstract class PropertyBinding
{
    public abstract void Save();
}

public class PropertyBinding<TControl, TValue> : PropertyBinding where TControl : MyGuiControlBase
{
    private readonly TControl _control;
    private readonly object _target;
    private readonly PropertyInfo _property;
    private readonly Func<TControl, TValue> _onSave;

    public PropertyBinding(TControl control, object target, PropertyInfo property, Func<TControl, TValue> getValueFromControl)
    {
        _control = control;
        _target = target;
        _property = property;
        _onSave = getValueFromControl;
    }

    public override void Save()
    {
        _property.SetValue(_target, _onSave(_control));
    }
}

public class GuiScreenSSGIConfig : MyGuiScreenBase
{
    private readonly List<PropertyBinding> _bindings = [];

    private readonly SSGIConfig _config;

    public GuiScreenSSGIConfig(SSGIConfig config)
        : base(new Vector2(0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.6f, 0.7f), false, null, MySandboxGame.Config.UIBkOpacity, MySandboxGame.Config.UIOpacity)
    {
        _config = config;

        EnabledBackgroundFade = true;
        m_closeOnEsc = true;
        m_drawEvenWithoutFocus = true;
        CanHideOthers = true;
        CanBeHidden = true;
        CloseButtonEnabled = true;
    }


    public override string GetFriendlyName() => GetType().FullName;

    public override void LoadContent()
    {
        base.LoadContent();
        RecreateControls(false);
    }

    public override void RecreateControls(bool constructor)
    {
        base.RecreateControls(constructor);

        float columnWidth = (Size!.Value.X - 0.1f) / 2;
        float rowHeight = 0.05f;

        var grid = new UniformGrid
        {
            ColumnWidth = columnWidth,
            RowHeight = rowHeight,
        };

        int row = 0;
        foreach (var property in GetBindingTargets())
        {
            _bindings.Add(CreateControl(property, grid, 0, row++));
        }

        grid.AddControlsToScreen(this, Vector2.Zero, false);

        AddCaption("SSGI Settings");

        // add footer buttons
        {
            float yPos = (Size!.Value.Y * 0.5f) - (MyGuiConstants.SCREEN_CAPTION_DELTA_Y / 2f);
            var button = new MyGuiControlButton(onButtonClick: OnSaveButtonClick)
            {
                Position = new Vector2(0, yPos),
                Text = "Save",
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM,
            };
            AddControl(button);
        }
    }

    private void OnSaveButtonClick(MyGuiControlButton btn)
    {
        _bindings.ForEach(i => i?.Save());
        _config.Save();
    }

    private IEnumerable<PropertyInfo> GetBindingTargets()
    {
        return typeof(SSGIConfig).GetProperties().Where(i => i.HasAttribute<ConfigPropertyAttribute>());
    }

    private PropertyBinding CreateControl(PropertyInfo prop, UniformGrid grid, int column, int row)
    {
        var attribute = prop.GetCustomAttribute<ConfigPropertyAttribute>();
        if (prop.PropertyType == typeof(bool))
        {
            grid.AddLabel(column, row, attribute.Name ?? prop.Name, HorizontalAlignment.Left);
            var control = grid.AddCheckbox(column + 1, row, attribute.Enabled, (bool)prop.GetValue(_config), attribute.ToolTip, HorizontalAlignment.Left);
            return new PropertyBinding<MyGuiControlCheckbox, bool>(control, _config, prop, checkbox => checkbox.IsChecked);
        }
        else if (prop.PropertyType == typeof(float) && prop.GetCustomAttribute<FloatConfigPropertyAttribute>() is FloatConfigPropertyAttribute floatProp)
        {
            grid.AddLabel(column, row, attribute.Name ?? prop.Name, HorizontalAlignment.Left);
            var control = grid.AddFloatSlider(column + 1, row, attribute.Enabled, (float)prop.GetValue(_config), floatProp.Min, floatProp.Max, floatProp.DefaultValue, true, HorizontalAlignment.Left);
            control.SetToolTip(floatProp.ToolTip);
            return new PropertyBinding<MyGuiControlSlider, float>(control, _config, prop, slider => slider.Value);
        }
        else if (prop.PropertyType == typeof(int) && prop.GetCustomAttribute<IntConfigPropertyAttribute>() is IntConfigPropertyAttribute intProp)
        {
            grid.AddLabel(column, row, attribute.Name ?? prop.Name, HorizontalAlignment.Left);
            var control = grid.AddIntegerSlider(column + 1, row, attribute.Enabled, (int)prop.GetValue(_config), intProp.Min, intProp.Max, intProp.DefaultValue, true, HorizontalAlignment.Left);
            control.SetToolTip(intProp.ToolTip);
            return new PropertyBinding<MyGuiControlSlider, int>(control, _config, prop, slider => (int)slider.Value);
        }
        else
        {
            return null;
        }
    }

}
