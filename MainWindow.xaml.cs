using ProductMasterPlanV1.Core;
using ProductMasterPlanV1.Core.Contract;
using ProductMasterPlanV1.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;


namespace ProductMasterPlanV1.Wpf
{
    public partial class MainWindow : Window
    {
        string letChangeTheWorld = string.Empty;
        private readonly IV1ApplicationService _v1ApplicationService;

        private V1ProjectionResponse? _currentProjection;
        private V1UserStatsResponse? _currentStats;

        private int _startAge;
        private decimal _afterTaxIncome;
        private int _yearsWillingToWork;
        private decimal _startingAssets;
        private decimal _startingDebt;
        private decimal? _budget;
        //private System.Windows.Threading.DispatcherTimer _debounceTimer;
        private readonly DispatcherTimer _budgetResultsDebounceTimer;
        private bool _isBudgetPreviewActive;
        private static readonly string PendingDisplayText = "•••";

        private bool _isBusy;

        public MainWindow(IV1ApplicationService v1ApplicationService)
        {

            InitializeComponent();
            _v1ApplicationService = v1ApplicationService ?? throw new ArgumentNullException(nameof(v1ApplicationService));

            _budgetResultsDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _budgetResultsDebounceTimer.Tick += BudgetResultsDebounceTimer_Tick;

            SeedDefaults();
            RefreshAllDisplays();
            SetStatus("Ready.");

            Loaded += MainWindow_Loaded; //TheEngineer
        }

        /*TheEngineer*/
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RunSimulationButton_Click(null, null);
        }

        private async void BudgetResultsDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _budgetResultsDebounceTimer.Stop();

            if (_isBusy)
                return;

            var errors = ValidateInputs();
            ShowValidationErrors(errors);

            if (errors.Count > 0)
            {
                SetStatus("Please fix validation errors.");
                return;
            }

            try
            {
                _isBusy = true;
                SetStatus("Calculating...");

                var request = BuildRunRequest();
                _currentProjection = await _v1ApplicationService.RunSimulationAsync(request);

                RefreshProjectionDisplay();
                SetStatus("Updated.");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void SeedDefaults()
        {
            UserKeyTextBox.Text = "user-1";
            NameTextBox.Text = "Nathan";
            PhoneTextBox.Text = "555-1234";
            EmailTextBox.Text = "nguyen_nathan@hotmail.com";

            _startAge = 30;
            _afterTaxIncome = 80000m;
            _yearsWillingToWork = 15;
            _startingAssets = 10000m;
            _startingDebt = 5000m;
            _budget = null;
        }

        private UserIdentity BuildUserIdentity()
        {
            return new UserIdentity
            {
                UserKey = UserKeyTextBox.Text?.Trim() ?? string.Empty,
                Name = NameTextBox.Text?.Trim() ?? string.Empty,
                Phone = PhoneTextBox.Text?.Trim() ?? string.Empty,
                Email = EmailTextBox.Text?.Trim() ?? string.Empty
            };
        }

        private RunV1SimulationRequest BuildRunRequest()
        {
            return new RunV1SimulationRequest
            {
                User = BuildUserIdentity(),
                Inputs = new V1Inputs
                {
                    StartAge = _startAge,
                    AfterTaxIncome = _afterTaxIncome,
                    YearsWillingToWork = _yearsWillingToWork,
                    StartingAssets = _startingAssets,
                    StartingDebt = _startingDebt,
                    Budget = _budget
                }
            };
        }

        private SaveChosenLifestyleRequest BuildSaveRequest()
        {
            var chosenBudget = _budget ?? (_currentProjection != null ? _currentProjection.SuggestedBudget : 0m);

            return new SaveChosenLifestyleRequest
            {
                User = BuildUserIdentity(),
                Inputs = new V1Inputs
                {
                    StartAge = _startAge,
                    AfterTaxIncome = _afterTaxIncome,
                    YearsWillingToWork = _yearsWillingToWork,
                    StartingAssets = _startingAssets,
                    StartingDebt = _startingDebt,
                    Budget = _budget
                },
                ChosenBudget = chosenBudget
            };
        }

        private List<string> ValidateInputs()
        {
            var errors = new List<string>();

            if (_startAge < 0) errors.Add("Age must be greater than or equal to 0.");
            if (_afterTaxIncome < 0) errors.Add("Income must be greater than or equal to 0.");
            if (_yearsWillingToWork < 0) errors.Add("Years must be greater than or equal to 0.");
            if (_startingAssets < 0) errors.Add("Assets should be greater than or equal to 0.");
            if (_budget.HasValue && _budget.Value < 0) errors.Add("Budget must be greater than or equal to 0.");
            if (string.IsNullOrWhiteSpace(UserKeyTextBox.Text)) errors.Add("User Key is required.");

            return errors;
        }

        private void ShowValidationErrors(List<string> errors)
        {
            ValidationSummaryTextBlock.Text = errors.Count == 0
                ? "No validation errors."
                : string.Join(Environment.NewLine, errors);

            ValidationSummaryBorder.Visibility = errors.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private async Task RunSimulationAsync()
        {

            if (_isBusy) return;

            var errors = ValidateInputs();
            ShowValidationErrors(errors);

            if (errors.Count > 0)
            {
                SetStatus("Please fix validation errors.");
                return;
            }

            try
            {
                _isBusy = true;
                SetStatus("Running simulation...");

                var request = BuildRunRequest();
                var projection = await _v1ApplicationService.RunSimulationAsync(request);

                if (projection == null)
                {
                    SetStatus("Simulation returned no result.");
                    return;
                }

                _currentProjection = projection;
                RefreshProjectionDisplay();
                SetStatus("Simulation complete.");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
            finally
            {
                _isBusy = false;
            }

            /**
            if (_isBusy) return;

            var errors = ValidateInputs();
            ShowValidationErrors(errors);

            if (errors.Count > 0)
            {
                SetStatus("Please fix validation errors.");
                return;
            }

            try
            {
                _isBusy = true;
                SetStatus("Running simulation...");

                var request = BuildRunRequest();
                _currentProjection = await _v1ApplicationService.RunSimulationAsync(request);

                RefreshProjectionDisplay();
                SetStatus("Simulation complete.");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
            finally
            {
                _isBusy = false;
            }
			**/
        }

        private async Task SaveAsync()
        {
            if (_isBusy) return;

            var errors = ValidateInputs();
            ShowValidationErrors(errors);

            if (errors.Count > 0)
            {
                SetStatus("Please fix validation errors.");
                return;
            }

            try
            {
                _isBusy = true;
                SetStatus("Saving...");

                if (!_budget.HasValue && _currentProjection == null)
                {
                    await RunSimulationAsync();
                }

                var request = BuildSaveRequest();
                await _v1ApplicationService.SaveChosenLifestyleAsync(request);

                _budget = request.ChosenBudget;
                RefreshAllDisplays();

                SetStatus("Saved");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async Task LoadStatsAsync()
        {
            if (_isBusy) return;

            try
            {
                _isBusy = true;
                SetStatus("Loading stats...");

                _currentStats = await _v1ApplicationService.GetUserStatsAsync(UserKeyTextBox.Text.Trim());
                RefreshStatsDisplay();

                SetStatus("Stats loaded.");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void RefreshAllDisplays()
        {
            AgeValueText.Text = _startAge.ToString(CultureInfo.InvariantCulture);
            IncomeValueText.Text = FormatMoney(_afterTaxIncome);
            YearsValueText.Text = _yearsWillingToWork.ToString(CultureInfo.InvariantCulture);
            AssetsValueText.Text = FormatMoney(_startingAssets);
            DebtValueText.Text = FormatMoney(_startingDebt);
            BudgetValueText.Text = _budget.HasValue ? FormatMoney(_budget.Value) : "System Managed";

            RefreshProjectionDisplay();
            RefreshStatsDisplay();
        }

        private void RefreshProjectionDisplay()
        {
            if (_currentProjection == null)
            {
                ActualBudgetValueText.Text = _budget.HasValue ? FormatMoney(_budget.Value) : ActualBudgetValueText.Text;
                return;
            }

            /* //TheEngineer
            if (_currentProjection?.FiAge != null)
            {
                //FIAgeValueText.Text = _currentProjection.FiAge.ToString(); //TheEngineer
                FIAgeValueText.Text = _currentProjection == null
                ? "-"
                : _currentProjection.FiAge.HasValue
                    ? _currentProjection.FiAge.Value.ToString()
                    : (_currentProjection.IsFiReachable ? "-" : "FI IS NOT POSSIBLE");
            }
			*/

            if (_currentProjection == null)
            {
                FIAgeValueText.Text = "-";
            }
            else if (_currentProjection.FiAge.HasValue)
            {
                FIAgeValueText.Text = _currentProjection.FiAge.Value.ToString();
            }
            else if (!_currentProjection.IsFiReachable)
            {
                // 🎮 Candy Crush moment
                FIAgeValueText.Text = "Congrats! You’ve unlocked: Work Forever Mode 🎉";

                // subtle visual feedback (reuse your existing animation)
                AnimateResultPulse(FIAgeValueText);

                // color shift to make it pop (no XAML change needed)
                FIAgeValueText.Foreground = new SolidColorBrush(Colors.OrangeRed);
            }
            else
            {
                FIAgeValueText.Text = "-";
            }

            SuggestedBudgetValueText.Text = FormatMoney(_currentProjection.SuggestedBudget);
            ActualBudgetValueText.Text = FormatMoney(_currentProjection.ActualBudget);
            //FIAgeValueText.Text = _currentProjection.FiAge.ToString();
            //FIAssetValueText.Text = _currentProjection.FiAsset is decimal fiAsset ? FormatMoney(fiAsset) : "-";
            MillionaireAgeValueText.Text = _currentProjection.MillionaireAge.ToString();

            AnimateResultPulse(SuggestedBudgetValueText);
            AnimateResultPulse(ActualBudgetValueText);
            AnimateResultPulse(FIAgeValueText);
            AnimateResultPulse(FIAssetValueText);
            AnimateResultPulse(MillionaireAgeValueText);
        }

        private void RefreshStatsDisplay()
        {
            DistinctUserCountValueText.Text = _currentStats?.DistinctUserCount.ToString() ?? "-";
            ReturnCountValueText.Text = _currentStats?.ReturnCount.ToString() ?? "-";
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("$#,##0", CultureInfo.InvariantCulture);
        }

        private void SetStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        private void AdjustAge(int delta) { _startAge += delta; RefreshAllDisplays(); }
        private void AdjustIncome(decimal delta) { _afterTaxIncome += delta; RefreshAllDisplays(); }
        private void AdjustYears(int delta) { _yearsWillingToWork += delta; RefreshAllDisplays(); }
        private void AdjustAssets(decimal delta) { _startingAssets += delta; RefreshAllDisplays(); }
        private void AdjustDebt(decimal delta) { _startingDebt += delta; RefreshAllDisplays(); }

        private void AdjustBudget(decimal delta)
        {
            var current = _budget ?? (_currentProjection != null ? _currentProjection.SuggestedBudget : 0m);

            _budget = current + delta;

            // Immediate UI update (Budget card updates instantly)
            RefreshAllDisplays();

            // Debounce simulation (Candy Crush "last click" behavior)
            _budgetResultsDebounceTimer.Stop();
            _budgetResultsDebounceTimer.Start();
        }

        private bool TryApplyVoiceCommand(string rawText, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(rawText))
            {
                error = "Voice command was empty.";
                return false;
            }

            var normalized = rawText.ToLowerInvariant()
                .Replace("$", string.Empty)
                .Replace(",", string.Empty)
                .Trim();

            normalized = Regex.Replace(normalized, @"\s+", " ");

            var match = Regex.Match(
                normalized,
                @"^(age|income|years|asset|assets|debt|budget)(?: set to)? (-?\d+)$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                error = "Invalid voice command.";
                return false;
            }

            var field = match.Groups[1].Value;
            var valueText = match.Groups[2].Value;

            if (!decimal.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                error = "Voice value was not a valid number.";
                return false;
            }

            switch (field)
            {
                case "age":
                    if (decimal.Truncate(value) != value)
                    {
                        error = "Age must be a whole number.";
                        return false;
                    }
                    _startAge = (int)value;
                    break;

                case "income":
                    _afterTaxIncome = value;
                    break;

                case "years":
                    if (decimal.Truncate(value) != value)
                    {
                        error = "Years must be a whole number.";
                        return false;
                    }
                    _yearsWillingToWork = (int)value;
                    break;

                case "asset":
                case "assets":
                    _startingAssets = value;
                    break;

                case "debt":
                    _startingDebt = value;
                    break;

                case "budget":
                    _budget = value;
                    break;
            }

            RefreshAllDisplays();
            return true;
        }

        private async void RunSimulationButton_Click(object sender, RoutedEventArgs e) => await RunSimulationAsync();
        private async void SaveButton_Click(object sender, RoutedEventArgs e) => await SaveAsync();
        private async void LoadStatsButton_Click(object sender, RoutedEventArgs e) => await LoadStatsAsync();

        private void ApplyVoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryApplyVoiceCommand(VoiceCommandTextBox.Text, out var error))
                SetStatus("Voice command applied.");
            else
                SetStatus(error);
        }

        private void AnimateResultPulse(System.Windows.Controls.TextBlock target)
        {
            if (target == null)
                return;

            if (target.RenderTransform is not System.Windows.Media.ScaleTransform scaleTransform)
            {
                scaleTransform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                target.RenderTransform = scaleTransform;
            }

            target.RenderTransformOrigin = new Point(0.5, 0.5);

            var storyboard = new System.Windows.Media.Animation.Storyboard
            {
                FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
            };

            var scaleXAnimation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            scaleXAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleXAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.16, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
            scaleXAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.98, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            scaleXAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
            System.Windows.Media.Animation.Storyboard.SetTarget(scaleXAnimation, target);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(
                scaleXAnimation,
                new PropertyPath("RenderTransform.ScaleX"));

            var scaleYAnimation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            scaleYAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleYAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.16, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
            scaleYAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.98, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            scaleYAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
            System.Windows.Media.Animation.Storyboard.SetTarget(scaleYAnimation, target);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(
                scaleYAnimation,
                new PropertyPath("RenderTransform.ScaleY"));

            var fontSizeAnimation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            fontSizeAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(target.FontSize, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fontSizeAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(target.FontSize + 4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
            fontSizeAnimation.KeyFrames.Add(
                new System.Windows.Media.Animation.EasingDoubleKeyFrame(target.FontSize, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
            System.Windows.Media.Animation.Storyboard.SetTarget(fontSizeAnimation, target);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(
                fontSizeAnimation,
                new PropertyPath(System.Windows.Controls.TextBlock.FontSizeProperty));

            storyboard.Children.Add(scaleXAnimation);
            storyboard.Children.Add(scaleYAnimation);
            storyboard.Children.Add(fontSizeAnimation);

            storyboard.Completed += (_, __) =>
            {
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
                target.FontSize = Math.Round(target.FontSize);
            };

            storyboard.Begin();
        }


        /*
                private void AnimateResultPulse(System.Windows.Controls.TextBlock target)
                {
                    if (target == null)
                        return;

                    if (target.RenderTransform is not System.Windows.Media.ScaleTransform scaleTransform)
                    {
                        scaleTransform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                        target.RenderTransform = scaleTransform;
                    }

                    target.RenderTransformOrigin = new Point(0.5, 0.5);

                    var storyboard = new System.Windows.Media.Animation.Storyboard();

                    var scaleXAnimation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
                    scaleXAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    scaleXAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
                    scaleXAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.95, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170))));
                    scaleXAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
                    System.Windows.Media.Animation.Storyboard.SetTarget(scaleXAnimation, target);
                    System.Windows.Media.Animation.Storyboard.SetTargetProperty(
                        scaleXAnimation,
                        new PropertyPath("RenderTransform.ScaleX"));

                    var scaleYAnimation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
                    scaleYAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    scaleYAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
                    scaleYAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.95, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170))));
                    scaleYAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
                    System.Windows.Media.Animation.Storyboard.SetTarget(scaleYAnimation, target);
                    System.Windows.Media.Animation.Storyboard.SetTargetProperty(
                        scaleYAnimation,
                        new PropertyPath("RenderTransform.ScaleY"));

                    var opacityAnimation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
                    opacityAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    opacityAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.70, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))));
                    opacityAnimation.KeyFrames.Add(
                        new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
                    System.Windows.Media.Animation.Storyboard.SetTarget(opacityAnimation, target);
                    System.Windows.Media.Animation.Storyboard.SetTargetProperty(
                        opacityAnimation,
                        new PropertyPath("Opacity"));

                    storyboard.Children.Add(scaleXAnimation);
                    storyboard.Children.Add(scaleYAnimation);
                    storyboard.Children.Add(opacityAnimation);
                    storyboard.Begin();
                }
        */

        private void AgeMinus6_Click(object sender, RoutedEventArgs e) => AdjustAge(-6);
        private void AgeMinus3_Click(object sender, RoutedEventArgs e) => AdjustAge(-3);
        private void AgeMinus1_Click(object sender, RoutedEventArgs e) => AdjustAge(-1);
        private void AgePlus1_Click(object sender, RoutedEventArgs e) => AdjustAge(1);
        private void AgePlus3_Click(object sender, RoutedEventArgs e) => AdjustAge(3);
        private void AgePlus6_Click(object sender, RoutedEventArgs e) => AdjustAge(6);

        private void IncomeMinus1000_Click(object sender, RoutedEventArgs e) => AdjustIncome(-1000m);
        private void IncomeMinus5000_Click(object sender, RoutedEventArgs e) => AdjustIncome(-5000m);
        private void IncomeMinus10000_Click(object sender, RoutedEventArgs e) => AdjustIncome(-10000m);
        private void IncomeMinus100000_Click(object sender, RoutedEventArgs e) => AdjustIncome(-100000m);
        private void IncomePlus1000_Click(object sender, RoutedEventArgs e) => AdjustIncome(1000m);
        private void IncomePlus5000_Click(object sender, RoutedEventArgs e) => AdjustIncome(5000m);
        private void IncomePlus10000_Click(object sender, RoutedEventArgs e) => AdjustIncome(10000m);
        private void IncomePlus100000_Click(object sender, RoutedEventArgs e) => AdjustIncome(100000m);

        private void YearsMinus6_Click(object sender, RoutedEventArgs e) => AdjustYears(-6);
        private void YearsMinus3_Click(object sender, RoutedEventArgs e) => AdjustYears(-3);
        private void YearsMinus1_Click(object sender, RoutedEventArgs e) => AdjustYears(-1);
        private void YearsPlus1_Click(object sender, RoutedEventArgs e) => AdjustYears(1);
        private void YearsPlus3_Click(object sender, RoutedEventArgs e) => AdjustYears(3);
        private void YearsPlus6_Click(object sender, RoutedEventArgs e) => AdjustYears(6);

        private void AssetsMinus1000_Click(object sender, RoutedEventArgs e) => AdjustAssets(-1000m);
        private void AssetsMinus5000_Click(object sender, RoutedEventArgs e) => AdjustAssets(-5000m);
        private void AssetsMinus10000_Click(object sender, RoutedEventArgs e) => AdjustAssets(-10000m);
        private void AssetsMinus100000_Click(object sender, RoutedEventArgs e) => AdjustAssets(-100000m);
        private void AssetsPlus1000_Click(object sender, RoutedEventArgs e) => AdjustAssets(1000m);
        private void AssetsPlus5000_Click(object sender, RoutedEventArgs e) => AdjustAssets(5000m);
        private void AssetsPlus10000_Click(object sender, RoutedEventArgs e) => AdjustAssets(10000m);
        private void AssetsPlus100000_Click(object sender, RoutedEventArgs e) => AdjustAssets(100000m);

        private void DebtMinus1000_Click(object sender, RoutedEventArgs e) => AdjustDebt(-1000m);
        private void DebtMinus5000_Click(object sender, RoutedEventArgs e) => AdjustDebt(-5000m);
        private void DebtMinus10000_Click(object sender, RoutedEventArgs e) => AdjustDebt(-10000m);
        private void DebtMinus100000_Click(object sender, RoutedEventArgs e) => AdjustDebt(-100000m);
        private void DebtPlus1000_Click(object sender, RoutedEventArgs e) => AdjustDebt(1000m);
        private void DebtPlus5000_Click(object sender, RoutedEventArgs e) => AdjustDebt(5000m);
        private void DebtPlus10000_Click(object sender, RoutedEventArgs e) => AdjustDebt(10000m);
        private void DebtPlus100000_Click(object sender, RoutedEventArgs e) => AdjustDebt(100000m);

        private void BudgetMinus1000_Click(object sender, RoutedEventArgs e) => AdjustBudget(-1000m);
        private void BudgetMinus5000_Click(object sender, RoutedEventArgs e) => AdjustBudget(-5000m);
        private void BudgetMinus10000_Click(object sender, RoutedEventArgs e) => AdjustBudget(-10000m);
        private void BudgetMinus100000_Click(object sender, RoutedEventArgs e) => AdjustBudget(-100000m);
        private void BudgetPlus1000_Click(object sender, RoutedEventArgs e) => AdjustBudget(1000m);
        private void BudgetPlus5000_Click(object sender, RoutedEventArgs e) => AdjustBudget(5000m);
        private void BudgetPlus10000_Click(object sender, RoutedEventArgs e) => AdjustBudget(10000m);
        private void BudgetPlus100000_Click(object sender, RoutedEventArgs e) => AdjustBudget(100000m);
    }
}