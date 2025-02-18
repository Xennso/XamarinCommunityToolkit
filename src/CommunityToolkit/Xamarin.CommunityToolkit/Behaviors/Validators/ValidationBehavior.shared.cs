﻿using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.CommunityToolkit.ObjectModel;
using Xamarin.Forms;

namespace Xamarin.CommunityToolkit.Behaviors.Internals
{
	/// <summary>
	/// The <see cref="ValidationBehavior"/> allows users to create custom validation behaviors. All of the validation behaviors in the Xamarin Community Toolkit inherit from this behavior, to expose a number of shared properties. Users can inherit from this class to create a custom validation behavior currently not supported through the Xamarin Community Toolkit. This behavios cannot be used directly as it's abstract.
	/// </summary>
	public abstract class ValidationBehavior : BaseBehavior<VisualElement>
	{
		public const string ValidVisualState = "Valid";

		public const string InvalidVisualState = "Invalid";

		public ValidationBehavior() => DefaultForceValidateCommand = new AsyncValueCommand(ForceValidate);

		/// <summary>
		/// Backing BindableProperty for the <see cref="IsEnabled"/> property.
		/// </summary>
		public static readonly BindableProperty IsEnabledProperty = 
			BindableProperty.Create(nameof(IsEnabledProperty), typeof(bool), typeof(SelectorInvalidValueValidator), defaultValue: true);
		 
		/// <summary>
		/// Backing BindableProperty for the <see cref="IsNotValid"/> property.
		/// </summary>
		public static readonly BindableProperty IsNotValidProperty =
			BindableProperty.Create(nameof(IsNotValid), typeof(bool), typeof(ValidationBehavior), false, BindingMode.OneWayToSource);

		/// <summary>
		/// Backing BindableProperty for the <see cref="IsValid"/> property.
		/// </summary>
		public static readonly BindableProperty IsValidProperty =
			BindableProperty.Create(nameof(IsValid), typeof(bool), typeof(ValidationBehavior), true, BindingMode.OneWayToSource, propertyChanged: OnIsValidPropertyChanged);

		/// <summary>
		/// Backing BindableProperty for the <see cref="IsRunning"/> property.
		/// </summary>
		public static readonly BindableProperty IsRunningProperty =
			BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(ValidationBehavior), false, BindingMode.OneWayToSource);

		/// <summary>
		/// Backing BindableProperty for the <see cref="ValidStyle"/> property.
		/// </summary>
		public static readonly BindableProperty ValidStyleProperty =
			BindableProperty.Create(nameof(ValidStyle), typeof(Style), typeof(ValidationBehavior), propertyChanged: OnValidationPropertyChanged);

		/// <summary>
		/// Backing BindableProperty for the <see cref="InvalidStyle"/> property.
		/// </summary>
		public static readonly BindableProperty InvalidStyleProperty =
			BindableProperty.Create(nameof(InvalidStyle), typeof(Style), typeof(ValidationBehavior), propertyChanged: OnValidationPropertyChanged);

		/// <summary>
		/// Backing BindableProperty for the <see cref="Flags"/> property.
		/// </summary>
		public static readonly BindableProperty FlagsProperty =
			BindableProperty.Create(nameof(Flags), typeof(ValidationFlags), typeof(ValidationBehavior), ValidationFlags.ValidateOnUnfocusing | ValidationFlags.ForceMakeValidWhenFocused, propertyChanged: OnValidationPropertyChanged);

		/// <summary>
		/// Backing BindableProperty for the <see cref="Value"/> property.
		/// </summary>
		public static readonly BindableProperty ValueProperty =
			BindableProperty.Create(nameof(Value), typeof(object), typeof(ValidationBehavior), propertyChanged: OnValuePropertyChanged);

		/// <summary>
		/// Backing BindableProperty for the <see cref="ValuePropertyName"/> property.
		/// </summary>
		public static readonly BindableProperty ValuePropertyNameProperty =
			BindableProperty.Create(nameof(ValuePropertyName), typeof(string), typeof(ValidationBehavior), defaultValueCreator: GetDefaultValuePropertyName, propertyChanged: OnValuePropertyNamePropertyChanged);

		/// <summary>
		/// Backing BindableProperty for the <see cref="ForceValidateCommand"/> property.
		/// </summary>
		public static readonly BindableProperty ForceValidateCommandProperty =
			BindableProperty.Create(nameof(ForceValidateCommand), typeof(ICommand), typeof(ValidationBehavior), defaultValueCreator: GetDefaultForceValidateCommand, defaultBindingMode: BindingMode.OneWayToSource);

		ValidationFlags currentStatus;

		bool isAttaching;

		BindingBase? defaultValueBinding;

		CancellationTokenSource? validationTokenSource;

		/// <summary>
		/// Indicates whether or not the current value is considered valid. This is a bindable property.
		/// </summary>
		public bool IsValid
		{
			get => (bool)GetValue(IsValidProperty);
			set => SetValue(IsValidProperty, value);
		}

		/// <summary>
		/// Indicates whether or not the validation is in progress now (waiting for an asynchronous call is finished).
		/// </summary>
		public bool IsRunning
		{
			get => (bool)GetValue(IsRunningProperty);
			set => SetValue(IsRunningProperty, value);
		}

		/// <summary>
		/// Indicates whether or not the current value is considered not valid. This is a bindable property.
		/// </summary>
		public bool IsNotValid
		{
			get => (bool)GetValue(IsNotValidProperty);
			set => SetValue(IsNotValidProperty, value);
		}

		/// <summary>
		/// Indicates whether or not validation is enabled. If set to false validation will return true without validating. This is a bindable property.
		/// </summary>
		public bool IsEnabled 
		{ 
			get => (bool)GetValue(IsEnabledProperty); 
			set => SetValue(IsEnabledProperty, value); 
		}
		
		/// <summary>
		/// The <see cref="Style"/> to apply to the element when validation is successful. This is a bindable property.
		/// </summary>
		public Style ValidStyle
		{
			get => (Style)GetValue(ValidStyleProperty);
			set => SetValue(ValidStyleProperty, value);
		}

		/// <summary>
		/// The <see cref="Style"/> to apply to the element when validation fails. This is a bindable property.
		/// </summary>
		public Style InvalidStyle
		{
			get => (Style)GetValue(InvalidStyleProperty);
			set => SetValue(InvalidStyleProperty, value);
		}

		/// <summary>
		/// Provides an enumerated value that specifies how to handle validation. This is a bindable property.
		/// </summary>
		public ValidationFlags Flags
		{
			get => (ValidationFlags)GetValue(FlagsProperty);
			set => SetValue(FlagsProperty, value);
		}

		/// <summary>
		/// The value to validate. This is a bindable property.
		/// </summary>
		public object? Value
		{
			get => GetValue(ValueProperty);
			set => SetValue(ValueProperty, value);
		}

		/// <summary>
		/// Allows the user to override the property that will be used as the value to validate. This is a bindable property.
		/// </summary>
		public string? ValuePropertyName
		{
			get => (string?)GetValue(ValuePropertyNameProperty);
			set => SetValue(ValuePropertyNameProperty, value);
		}

		/// <summary>
		/// Allows the user to provide a custom <see cref="ICommand"/> that handles forcing validation. This is a bindable property.
		/// </summary>
		public ICommand? ForceValidateCommand
		{
			get => (ICommand?)GetValue(ForceValidateCommandProperty);
			set => SetValue(ForceValidateCommandProperty, value);
		}

		protected virtual string DefaultValuePropertyName => Entry.TextProperty.PropertyName;

		protected virtual ICommand DefaultForceValidateCommand { get; }

		/// <summary>
		/// Forces the behavior to make a validation pass.
		/// </summary>
		public ValueTask ForceValidate() => UpdateStateAsync(true);

		internal ValueTask ValidateNestedAsync(CancellationToken token) => UpdateStateAsync(true, token);

		protected virtual object? Decorate(object? value) => value;

		protected abstract ValueTask<bool> ValidateAsync(object? value, CancellationToken token);

		protected override async void OnAttachedTo(VisualElement bindable)
		{
			base.OnAttachedTo(bindable);

			isAttaching = true;
			currentStatus = ValidationFlags.ValidateOnAttaching;

			OnValuePropertyNamePropertyChanged();
			await UpdateStateAsync(false);
			isAttaching = false;
		}

		protected override void OnDetachingFrom(VisualElement bindable)
		{
			if (defaultValueBinding != null)
			{
				RemoveBinding(ValueProperty);
				defaultValueBinding = null;
			}

			currentStatus = ValidationFlags.None;
			base.OnDetachingFrom(bindable);
		}

		protected override async void OnViewPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			base.OnViewPropertyChanged(sender, e);
			if (e.PropertyName == VisualElement.IsFocusedProperty.PropertyName)
			{
				currentStatus = View?.IsFocused is true
					? ValidationFlags.ValidateOnFocusing
					: ValidationFlags.ValidateOnUnfocusing;
				await UpdateStateAsync(false);
			}
		}

		protected static async void OnValidationPropertyChanged(BindableObject bindable, object oldValue, object newValue)
			=> await ((ValidationBehavior)bindable).UpdateStateAsync(false);

		static void OnIsValidPropertyChanged(BindableObject bindable, object oldValue, object newValue)
			=> ((ValidationBehavior)bindable).OnIsValidPropertyChanged();

		static void OnValuePropertyChanged(BindableObject bindable, object oldValue, object newValue)
		{
			((ValidationBehavior)bindable).OnValuePropertyChanged();
			OnValidationPropertyChanged(bindable, oldValue, newValue);
		}

		static void OnValuePropertyNamePropertyChanged(BindableObject bindable, object oldValue, object newValue)
			=> ((ValidationBehavior)bindable).OnValuePropertyNamePropertyChanged();

		static object GetDefaultForceValidateCommand(BindableObject bindable)
			=> ((ValidationBehavior)bindable).DefaultForceValidateCommand;

		static object GetDefaultValuePropertyName(BindableObject bindable)
			=> ((ValidationBehavior)bindable).DefaultValuePropertyName;

		void OnIsValidPropertyChanged()
			=> IsNotValid = !IsValid;

		void OnValuePropertyChanged()
		{
			if (isAttaching)
				return;

			currentStatus = ValidationFlags.ValidateOnValueChanging;
		}

		void OnValuePropertyNamePropertyChanged()
		{
			if (IsBound(ValueProperty, defaultValueBinding))
			{
				defaultValueBinding = null;
				return;
			}

			defaultValueBinding = new Binding
			{
				Path = ValuePropertyName,
				Source = View
			};
			SetBinding(ValueProperty, defaultValueBinding);
		}

		async ValueTask UpdateStateAsync(bool isForced, CancellationToken? parentToken = null)
		{
			if ((View?.IsFocused ?? false) && Flags.HasFlag(ValidationFlags.ForceMakeValidWhenFocused))
			{
				IsRunning = true;
				ResetValidationTokenSource(null);
				IsValid = true;
				IsRunning = false;
			}
			else if (isForced || (currentStatus != ValidationFlags.None && Flags.HasFlag(currentStatus)))
			{
				IsRunning = true;
				using var tokenSource = new CancellationTokenSource();
				var token = parentToken ?? tokenSource.Token;
				ResetValidationTokenSource(tokenSource);

				try
				{
					var isValid = !IsEnabled ? true : await ValidateAsync(Decorate(Value), token).ConfigureAwait(false);
					validationTokenSource = null;

					if (token.IsCancellationRequested)
						return;

					IsValid = isValid;
					IsRunning = false;
				}
				catch (TaskCanceledException)
				{
					return;
				}
			}

			UpdateStyle();
		}

		void UpdateStyle()
		{
			if (View == null)
				return;

			VisualStateManager.GoToState(View, IsValid ? ValidVisualState : InvalidVisualState);

			if ((ValidStyle ?? InvalidStyle) == null)
				return;

			View.Style = IsValid ? ValidStyle : InvalidStyle;
		}

		void ResetValidationTokenSource(CancellationTokenSource? newTokenSource)
		{
			validationTokenSource?.Cancel();
			validationTokenSource = newTokenSource;
		}
	}
}
