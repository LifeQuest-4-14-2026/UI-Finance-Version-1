using ProductMasterPlanV1.Core;
using ProductMasterPlanV1.Core.Contract;
using ProductMasterPlanV1.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;


namespace ProductMasterPlanV1.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly IV1ApplicationService _v1ApplicationService;

        private V1ProjectionResponse? _currentProjection;
        private V1UserStatsResponse? _currentStats;

        private int _startAge;
        private decimal _afterTaxIncome;
        private int _yearsWillingToWork;
        private decimal _startingAssets;
        private decimal _startingDebt;
        private decimal? _budget;
        private const decimal FiHorizonAge = 100m;   //4/16/2026 for use in UI match with business private const decimal FiHorizonAge = 100m;
                                                     //private System.Windows.Threading.DispatcherTimer _debounceTimer;

        private const int MinAge = 0;
        private const int MaxAge = 100;

        private const decimal MinIncome = 0m;
        private const decimal MinAssets = 0m;
        private const decimal MinDebt = 0m;

        private readonly DispatcherTimer _budgetResultsDebounceTimer;
        private bool _isBudgetPreviewActive;
        //private static readonly string PendingDisplayText = "•••";

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
            //SetStatus("Ready.");
            //MessageBox.Show("CTOR");
            Loaded += MainWindow_Loaded; //TheEngineer
        }

        /*TheEngineer*/
        //private void MainWindow_Loaded(object sender, RoutedEventArgs e) //4/17/2026
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("LOADED");
            RunSimulationButton_Click(null, null);
        }

        private async void VoiceButton_Click(object sender, RoutedEventArgs e)
        {
            await StartVoiceOnce(); //4/17/2026
        }

        //4/17/2026
        private async Task StartVoiceOnce()
        {
            var config = SpeechConfig.FromSubscription("xxxxxx", "eastus");
            config.SpeechRecognitionLanguage = "en-US";

            using var recognizer = new SpeechRecognizer(config);

            var result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                var spoken = result.Text.ToLowerInvariant();

                if (spoken.Contains("income up"))
                {
                    AdjustIncomeValue(10000m);
                }
                else
                {
                    MessageBox.Show($"You said: {result.Text}");
                }
            }
            else
            {
                MessageBox.Show($"No speech recognized: {result.Reason}");
            }
        }



        private async void BudgetResultsDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _budgetResultsDebounceTimer.Stop();
            await RunSimulationAsync();
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
            //MessageBox.Show("RUNSIM");
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

                //MessageBox.Show(
                //    $"SuggestedBudget={projection?.SuggestedBudget}\n" +
                //    $"ActualBudget={projection?.ActualBudget}\n" +
                //    $"FiAge={projection?.FiAge}\n" +
                //    $"FiAsset={projection?.FiAsset}\n" +
                //    $"MillionaireAge={projection?.MillionaireAge}");

                if (projection == null)
                {
                    SetStatus("Simulation returned no result.");
                    return;
                }

                _currentProjection = projection;
                RefreshProjectionDisplay();
                /*4-20-2026
		        if (projection.IsBudgetTooLow)
                {
                    SetStatus("Low Budget Is Selected!");
                }
                else
                {
                    //SetStatus("Simulation complete.");
                }
				*/
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

        /*4-20-2026
        private void RefreshProjectionDisplay()
        {
            if (_currentProjection == null)
            {
                ClearProjectionDisplay();
                return;
            }

            var projection = _currentProjection;

            SuggestedBudgetValueText.Text = FormatMoney(projection.SuggestedBudget);

            RenderActualBudget(projection);
            RenderFiAge(projection);
            RenderFiAsset(projection);
            RenderMillionaireAge(projection);
            RenderStatusMessage(projection);
        }
		*/

        private void RefreshProjectionDisplay()
        {
            var projection = _currentProjection;
            if (projection == null)
            {
                ClearProjectionDisplay();
                return;
            }

            SuggestedBudgetValueText.Text = FormatMoney(projection.SuggestedBudget);
            RenderActualBudget(projection);
            RenderFiAge(projection);
            RenderFiAsset(projection);
            RenderMillionaireAge(projection);
            RenderStatusMessage(projection);
        }
        //4-20-2026
        private void ClearProjectionDisplay()
        {
            SuggestedBudgetValueText.Text = "-";
            ActualBudgetValueText.Text = _budget.HasValue ? FormatMoney(_budget.Value) : "-";
            FIAgeValueText.Text = "-";
            FIAssetValueText.Text = "-";
            MillionaireAgeValueText.Text = "-";
            RealityMessageValueText.Text = "-";
            SetStatus(string.Empty);
        }

        private void RenderStatusMessage(V1ProjectionResponse projection)
        {
            SetStatus(string.IsNullOrWhiteSpace(projection.Message)
                ? "Simulation complete."
                : projection.Message);

            SetStatus(projection.Message);
            RealityMessageValueText.Text = projection.Message;
            RealityMessageValueText.Foreground = projection.IsIncomeTooLow
                ? Brushes.DarkRed
                : Brushes.Black;
        }

        private void RenderActualBudget(V1ProjectionResponse projection)
        {
            if (projection.IsBudgetTooLow)
            {
                ActualBudgetValueText.Text = $"{FormatMoney(projection.ActualBudget)}  |  SURVIVOR MODE";
                AnimateResultPulse(ActualBudgetValueText);
                return;
            }

            ActualBudgetValueText.Text = FormatMoney(projection.ActualBudget);
        }

        /*
        private void RenderFiAge(V1ProjectionResponse projection)
        {
            if (!projection.IsFiReachable || !projection.FiAge.HasValue)
            {
                FIAgeValueText.Text = "WORK TILL YOU DROP!";
                AnimateResultPulse(FIAgeValueText);
                return;
            }

            var fiAge = projection.FiAge.Value;

            if (fiAge <= 67)
            {
                FIAgeValueText.Text = fiAge.ToString();
            }
            else if (fiAge <= 75)
            {
                FIAgeValueText.Text = $"{fiAge}  |  BAND 1";
                AnimateResultPulse(FIAgeValueText);
            }
            else if (fiAge <= 79)
            {
                FIAgeValueText.Text = $"{fiAge}  |  BAND 2";
                AnimateResultPulse(FIAgeValueText);
            }
            else if (fiAge <= 89)
            {
                FIAgeValueText.Text = $"{fiAge}  |  BAND 3";
                AnimateResultPulse(FIAgeValueText);
            }
            else
            {
                FIAgeValueText.Text = $"{fiAge}  |  BAND 4 MUTANT";
                AnimateResultPulse(FIAgeValueText);
            }
        }
		*/

        private void RenderFiAge(V1ProjectionResponse projection)
        {
            if (!projection.IsFiReachable || !projection.FiAge.HasValue)
            {
                FIAgeValueText.Text = "WORK TILL YOU DROP!";
                AnimateResultPulse(FIAgeValueText);
                return;
            }

            var fiAgeText = projection.FiAge.Value.ToString();

            switch (projection.FiRealityBand)
            {
                case FiRealityBand.Normal:
                    FIAgeValueText.Text = fiAgeText;
                    break;

                case FiRealityBand.Extended:
                    FIAgeValueText.Text = $"{fiAgeText}  |  EXTENDED";
                    break;

                case FiRealityBand.LateLife:
                    FIAgeValueText.Text = $"{fiAgeText}  |  LATE LIFE";
                    AnimateResultPulse(FIAgeValueText);
                    break;

                case FiRealityBand.Elderly:
                    FIAgeValueText.Text = $"{fiAgeText}  |  ELDERLY";
                    AnimateResultPulse(FIAgeValueText);
                    break;

                case FiRealityBand.Mutant:
                    FIAgeValueText.Text = "🧬 MUTANT PLAN";
                    AnimateResultPulse(FIAgeValueText);
                    break;
            }
        }

        private void RenderFiAsset(V1ProjectionResponse projection)
        {
            if (!projection.IsFiReachable)
            {
                FIAssetValueText.Text = "NEVER HAPPENS!";
                AnimateResultPulse(FIAssetValueText);
                return;
            }

            FIAssetValueText.Text = projection.FiAsset.HasValue
                ? FormatMoney(projection.FiAsset.Value)
                : "-";
        }

        private void RenderMillionaireAge(V1ProjectionResponse projection)
        {
            MillionaireAgeValueText.Text = projection.MillionaireAge.HasValue
                ? projection.MillionaireAge.Value.ToString()
                : "-";
        }

        //4-20-2026
        /*
        private void ApplyCandyCrushUI(V1ProjectionResponse projection)
        {
            if (projection.FiAge == null)
            {
                FIAgeValueText.Text = "WORK TILL YOU DROP!";
                FIAssetValueText.Text = "NEVER HAPPENS!";
                AnimateResultPulse(FIAgeValueText);
                return;
            }

            var fiAge = projection.FiAge.Value;

            if (fiAge <= 67)
            {
                // Normal
                FIAgeValueText.Text = fiAge.ToString();
            }
            else if (fiAge <= 75)
            {
                FIAgeValueText.Text = $"{fiAge} (Extended)";
            }
            else if (fiAge <= 79)
            {
                FIAgeValueText.Text = $"{fiAge} 😬";
                AnimateResultPulse(FIAgeValueText);
            }
            else if (fiAge <= 89)
            {
                FIAgeValueText.Text = $"{fiAge} 😵";
                AnimateResultPulse(FIAgeValueText);
            }
            else
            {
                FIAgeValueText.Text = "🧬 MUTANT PLAN";
                AnimateResultPulse(FIAgeValueText);
            }
        }
		*/

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

        private void AdjustYears(int delta) { _yearsWillingToWork += delta; RefreshAllDisplays(); }


        private void AdjustAgeValue(int delta)
        {
            var next = _startAge + delta;
            _startAge = Math.Max(MinAge, Math.Min(MaxAge, next));

            RefreshAllDisplays();

            _budgetResultsDebounceTimer.Stop();
            _budgetResultsDebounceTimer.Start();
        }


        private void AdjustIncomeValue(decimal delta)
        {
            if (!_budget.HasValue && _currentProjection != null)
            {
                _budget = _currentProjection.ActualBudget;
            }

            var next = _afterTaxIncome + delta;
            _afterTaxIncome = Math.Max(0m, next);

            RefreshAllDisplays();

            _budgetResultsDebounceTimer.Stop();
            _budgetResultsDebounceTimer.Start();
        }


        private void AdjustAssetsValue(decimal delta)
        {
            var next = _startingAssets + delta;
            _startingAssets = Math.Max(MinAssets, next);

            RefreshAllDisplays();

            _budgetResultsDebounceTimer.Stop();
            _budgetResultsDebounceTimer.Start();
        }

        private void AdjustDebtValue(decimal delta)
        {
            var next = _startingDebt + delta;
            _startingDebt = Math.Max(MinDebt, next);

            RefreshAllDisplays();

            _budgetResultsDebounceTimer.Stop();
            _budgetResultsDebounceTimer.Start();
        }



        private void AdjustBudget(decimal delta)
        {
            try
            {
                var current = _budget ?? (_currentProjection?.SuggestedBudget ?? 0m);
                var next = current + delta;
                var max = _afterTaxIncome > 0 ? _afterTaxIncome : decimal.MaxValue;
                _budget = Math.Min(max, Math.Max(0m, next));

                RefreshAllDisplays();

                _budgetResultsDebounceTimer.Stop();
                _budgetResultsDebounceTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ADJUST_BUDGET_EXCEPTION");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                throw;
            }
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

        private void AgeMinus6_Click(object sender, RoutedEventArgs e) => AdjustAgeValue(-6);
        private void AgeMinus3_Click(object sender, RoutedEventArgs e) => AdjustAgeValue(-3);
        private void AgeMinus1_Click(object sender, RoutedEventArgs e) => AdjustAgeValue(-1);
        private void AgePlus1_Click(object sender, RoutedEventArgs e) => AdjustAgeValue(1);
        private void AgePlus3_Click(object sender, RoutedEventArgs e) => AdjustAgeValue(3);
        private void AgePlus6_Click(object sender, RoutedEventArgs e) => AdjustAgeValue(6);


        private void YearsMinus6_Click(object sender, RoutedEventArgs e) => AdjustYearsValue(-6);
        private void YearsMinus3_Click(object sender, RoutedEventArgs e) => AdjustYearsValue(-3);
        private void YearsMinus1_Click(object sender, RoutedEventArgs e) => AdjustYearsValue(-1);
        private void YearsPlus1_Click(object sender, RoutedEventArgs e) => AdjustYearsValue(1);
        private void YearsPlus3_Click(object sender, RoutedEventArgs e) => AdjustYearsValue(3);
        private void YearsPlus6_Click(object sender, RoutedEventArgs e) => AdjustYearsValue(6);

        private void AdjustYearsValue(int delta)
        {
            var next = _yearsWillingToWork + delta;
            //_yearsWillingToWork = Math.Max(0, next);
            _yearsWillingToWork = (int)Math.Max(0m, Math.Min(FiHorizonAge, next));

            RefreshAllDisplays();

            _budgetResultsDebounceTimer.Stop();
            _budgetResultsDebounceTimer.Start();
        }

        private void BudgetMinus1000_Click(object sender, RoutedEventArgs e) => AdjustBudget(-1000m);
        private void BudgetMinus5000_Click(object sender, RoutedEventArgs e) => AdjustBudget(-5000m);
        private void BudgetMinus10000_Click(object sender, RoutedEventArgs e) => AdjustBudget(-10000m);
        private void BudgetMinus100000_Click(object sender, RoutedEventArgs e) => AdjustBudget(-100000m);
        private void BudgetPlus1000_Click(object sender, RoutedEventArgs e) => AdjustBudget(1000m);
        private void BudgetPlus5000_Click(object sender, RoutedEventArgs e) => AdjustBudget(5000m);
        private void BudgetPlus10000_Click(object sender, RoutedEventArgs e) => AdjustBudget(10000m);
        private void BudgetPlus100000_Click(object sender, RoutedEventArgs e) => AdjustBudget(100000m);

        private void IncomeMinus1000_Click(object sender, RoutedEventArgs e) => AdjustIncomeValue(-1000m);
        private void IncomeMinus5000_Click(object sender, RoutedEventArgs e) => AdjustIncomeValue(-5000m);
        private void IncomeMinus10000_Click(object sender, RoutedEventArgs e) => AdjustIncomeValue(-10000m);
        private void IncomeMinus100000_Click(object sender, RoutedEventArgs e) => AdjustIncomeValue(-100000m);
        private void IncomePlus1000_Click(object sender, RoutedEventArgs e) => AdjustIncomeValue(1000m);
        private void IncomePlus5000_Click(object sender, RoutedEventArgs e) => AdjustIncomeValue(5000m);
        private void IncomePlus10000_Click(object sender, RoutedEventArgs e) => AdjustIncomeValue(10000m);
        private void IncomePlus100000_Click(object sender, RoutedEventArgs e) => AdjustIncomeValue(100000m);

        private void AssetsMinus1000_Click(object sender, RoutedEventArgs e) => AdjustAssetsValue(-1000m);
        private void AssetsMinus5000_Click(object sender, RoutedEventArgs e) => AdjustAssetsValue(-5000m);
        private void AssetsMinus10000_Click(object sender, RoutedEventArgs e) => AdjustAssetsValue(-10000m);
        private void AssetsMinus100000_Click(object sender, RoutedEventArgs e) => AdjustAssetsValue(-100000m);
        private void AssetsPlus1000_Click(object sender, RoutedEventArgs e) => AdjustAssetsValue(1000m);
        private void AssetsPlus5000_Click(object sender, RoutedEventArgs e) => AdjustAssetsValue(5000m);
        private void AssetsPlus10000_Click(object sender, RoutedEventArgs e) => AdjustAssetsValue(10000m);
        private void AssetsPlus100000_Click(object sender, RoutedEventArgs e) => AdjustAssetsValue(100000m);

        private void DebtMinus1000_Click(object sender, RoutedEventArgs e) => AdjustDebtValue(-1000m);
        private void DebtMinus5000_Click(object sender, RoutedEventArgs e) => AdjustDebtValue(-5000m);
        private void DebtMinus10000_Click(object sender, RoutedEventArgs e) => AdjustDebtValue(-10000m);
        private void DebtMinus100000_Click(object sender, RoutedEventArgs e) => AdjustDebtValue(-100000m);
        private void DebtPlus1000_Click(object sender, RoutedEventArgs e) => AdjustDebtValue(1000m);
        private void DebtPlus5000_Click(object sender, RoutedEventArgs e) => AdjustDebtValue(5000m);
        private void DebtPlus10000_Click(object sender, RoutedEventArgs e) => AdjustDebtValue(10000m);
        private void DebtPlus100000_Click(object sender, RoutedEventArgs e) => AdjustDebtValue(100000m);
    }
}
