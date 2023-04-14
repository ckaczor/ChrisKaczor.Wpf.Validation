using DebounceThrottle;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChrisKaczor.Wpf.Validation;

public class BindingExpressionInfo
{
    public FrameworkElement FrameworkElement { get; }
    public BindingExpression BindingExpression { get; }

    public BindingExpressionInfo(FrameworkElement frameworkElement, BindingExpression bindingExpression)
    {
        FrameworkElement = frameworkElement;
        BindingExpression = bindingExpression;
    }
}

public static class WindowExtensions
{
    public static List<BindingExpressionInfo> GetBindingExpressions(this DependencyObject parent)
    {
        return GetBindingExpressions(parent, null);
    }

    public static List<BindingExpressionInfo> GetBindingExpressions(this DependencyObject parent, UpdateSourceTrigger[]? triggers)
    {
        // Create a list of framework elements and binding expressions
        var bindingExpressions = new List<BindingExpressionInfo>();

        // Get all explicit bindings into the list
        GetBindingExpressions(parent, triggers, ref bindingExpressions);

        return bindingExpressions;
    }

    private static void GetBindingExpressions(DependencyObject parent, UpdateSourceTrigger[]? triggers, ref List<BindingExpressionInfo> bindingExpressions)
    {
        // Get the number of children
        var childCount = VisualTreeHelper.GetChildrenCount(parent);

        // Loop over each child
        for (var childIndex = 0; childIndex < childCount; childIndex++)
        {
            // Get the child
            var dependencyObject = VisualTreeHelper.GetChild(parent, childIndex);

            // Check if the object is a tab control
            if (dependencyObject is TabControl tabControl)
            {
                // Loop over each tab
                foreach (TabItem tabItem in tabControl.Items)
                    GetBindingExpressions((DependencyObject) tabItem.Content, triggers, ref bindingExpressions);
            }
            else
            {
                // Cast to framework element
                if (dependencyObject is FrameworkElement frameworkElement)
                {
                    // Get the list of properties
                    IEnumerable<DependencyProperty> dependencyProperties = TypeDescriptor.GetProperties(frameworkElement)
                        .Cast<PropertyDescriptor>()
                        .Select(DependencyPropertyDescriptor.FromProperty)
                        .Where(dependencyPropertyDescriptor => dependencyPropertyDescriptor != null)
                        .Select(dependencyPropertyDescriptor => dependencyPropertyDescriptor.DependencyProperty)
                        .ToList();

                    // Loop over each dependency property in the list
                    foreach (var dependencyProperty in dependencyProperties)
                    {
                        // Try to get the binding expression for the property
                        var bindingExpression = frameworkElement.GetBindingExpression(dependencyProperty);

                        // If there is a binding expression and it is set to explicit then make it update the source
                        if (bindingExpression != null && (triggers == null || triggers.Contains(bindingExpression.ParentBinding.UpdateSourceTrigger)))
                            bindingExpressions.Add(new BindingExpressionInfo(frameworkElement, bindingExpression));
                    }
                }

                // If the dependency object has any children then check them
                if (VisualTreeHelper.GetChildrenCount(dependencyObject) > 0)
                    GetBindingExpressions(dependencyObject, triggers, ref bindingExpressions);
            }
        }
    }

    public static void UpdateAllSources(this DependencyObject _, IEnumerable<BindingExpressionInfo> bindingExpressions)
    {
        foreach (var expression in bindingExpressions)
            expression.BindingExpression.UpdateSource();
    }

    public static void ClearAllValidationErrors(this DependencyObject _, IEnumerable<BindingExpressionInfo> bindingExpressions)
    {
        foreach (var expression in bindingExpressions)
            System.Windows.Controls.Validation.ClearInvalid(expression.BindingExpression);
    }

    public static bool IsValid(this DependencyObject window)
    {
        return IsValid(window, null);
    }

    private static readonly DebounceDispatcher FocusDispatcher = new(50);

    public static bool IsValid(this DependencyObject window, TabControl? tabControl)
    {
        // Get a list of all framework elements and binding expressions
        var bindingExpressions = window.GetBindingExpressions();

        // Loop over each binding expression and clear any existing error
        window.ClearAllValidationErrors(bindingExpressions);

        // Force all explicit bindings to update the source
        window.UpdateAllSources(bindingExpressions);

        // See if there are any errors
        if (!bindingExpressions.Any(b => b.BindingExpression.HasError))
            return true;

        // Get the first framework element with an error
        var firstErrorElement = bindingExpressions.First(b => b.BindingExpression.HasError).FrameworkElement;

        if (tabControl == null)
        {
            // Set focus
            firstErrorElement.Focus();

            return false;
        }

        // Loop over each tab item
        foreach (TabItem tabItem in tabControl.Items)
        {
            // Cast the content as visual
            var content = (Visual) tabItem.Content;

            // See if the control with the error is a descendant 
            if (!firstErrorElement.IsDescendantOf(content))
                continue;

            // Select the tab
            tabItem.IsSelected = true;
        }

        var dispatcher = Dispatcher.CurrentDispatcher;
        FocusDispatcher.Debounce(() => dispatcher.Invoke(() => firstErrorElement.Focus()));

        return false;
    }
}