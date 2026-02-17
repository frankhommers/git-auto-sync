# Modern UI Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the basic stacked layout with a modern sidebar-based UI using Avalonia's built-in Fluent theme resources.

**Architecture:** Pure Fluent theme approach. Replace all hardcoded hex colors with `{DynamicResource}` references. Switch from vertical scroll layout to a sidebar (repo list) + content area (detail + log) grid. System-following dark/light mode.

**Tech Stack:** Avalonia 11.3.12, FluentTheme (already included), ReactiveUI, no new packages.

---

### Task 1: Add custom status color resources to App.axaml

**Files:**
- Modify: `GitAutoSync.GUI/App.axaml`

**Step 1: Add app-level resources**

Add a `<Application.Resources>` block with custom status brushes. These are the only custom colors in the app - everything else uses theme resources.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:GitAutoSync.GUI"
             x:Class="GitAutoSync.GUI.App"
             RequestedThemeVariant="Default"
             >

    <NativeMenu.Menu>
        <NativeMenu>
            <NativeMenuItem Header="About Git Auto Sync" Click="AboutMenuItem_OnClick" />
            <NativeMenuItemSeparator />
            <NativeMenuItem Header="Services" />
            <NativeMenuItemSeparator />
            <NativeMenuItem Header="Hide Git Auto Sync" Gesture="Cmd+H" />
            <NativeMenuItem Header="Hide Others" Gesture="Cmd+Alt+H" />
            <NativeMenuItem Header="Show All" />
            <NativeMenuItemSeparator />
            <NativeMenuItem Header="Quit Git Auto Sync" Gesture="Cmd+Q" Click="QuitMenuItem_OnClick" />
        </NativeMenu>
    </NativeMenu.Menu>

    <Application.Resources>
        <SolidColorBrush x:Key="StatusRunningBrush" Color="#48BB78" />
        <SolidColorBrush x:Key="StatusErrorBrush" Color="#F56565" />
        <SolidColorBrush x:Key="StatusStoppedBrush" Color="#718096" />
    </Application.Resources>

    <Application.DataTemplates>
        <local:ViewLocator />
    </Application.DataTemplates>

    <Application.Styles>
        <FluentTheme />
    </Application.Styles>

</Application>
```

**Step 2: Build to verify no errors**

Run: `dotnet build GitAutoSync.GUI/GitAutoSync.GUI.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add GitAutoSync.GUI/App.axaml
git commit -m "feat(ui): add custom status color resources to App.axaml"
```

---

### Task 2: Update RepositoryViewModel to use brush resources instead of hex strings

**Files:**
- Modify: `GitAutoSync.GUI/ViewModels/RepositoryViewModel.cs`

**Step 1: Replace StatusColor string property with StatusBrushKey string property**

The ViewModel will expose a resource key name instead of a hex color. The XAML will use this via a converter or we simply bind the Ellipse fill directly based on status. Actually, the simplest approach: keep `StatusColor` as a string hex but use it only for the status dot `Ellipse.Fill`. The XAML will use `SolidColorBrush` binding. But to be theme-aware, change `StatusColor` to return resource key names and use a resource lookup in XAML.

Simplest correct approach: keep `StatusColor` returning hex strings (they're status-specific, not theme colors), but make sure the repo item template uses theme resources for everything else. The status colors are semantic (green/red/grey) and don't need to change between light/dark.

**No changes needed to RepositoryViewModel.cs** - the status hex colors are correct as-is since they represent semantic status colors, not theme colors. The XAML template will handle using theme resources for text and backgrounds.

**Step 2: Build to verify**

Run: `dotnet build GitAutoSync.GUI/GitAutoSync.GUI.csproj`
Expected: Build succeeded

---

### Task 3: Rewrite MainWindow.axaml with sidebar layout and theme resources

**Files:**
- Modify: `GitAutoSync.GUI/Views/MainWindow.axaml`

This is the main task. Replace the entire XAML with the new sidebar layout.

**Step 1: Write the new MainWindow.axaml**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:GitAutoSync.GUI.ViewModels"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d" d:DesignWidth="1000" d:DesignHeight="650"
        x:Class="GitAutoSync.GUI.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="Git Auto Sync"
        Icon="avares://GitAutoSync.GUI/Assets/icon.png"
        MinWidth="800"
        MinHeight="500"
        Width="1000"
        Height="650">

    <Design.DataContext>
        <vm:MainWindowViewModel />
    </Design.DataContext>

    <Grid RowDefinitions="*,Auto">

        <!-- Main content: Sidebar + Content -->
        <Grid Grid.Row="0" ColumnDefinitions="280,Auto,*">

            <!-- ==================== SIDEBAR ==================== -->
            <Border Grid.Column="0"
                    Background="{DynamicResource LayerFillColorDefaultBrush}"
                    Padding="0">
                <Grid RowDefinitions="Auto,*,Auto">

                    <!-- Sidebar Header -->
                    <Border Grid.Row="0" Padding="16,16,16,8">
                        <Grid RowDefinitions="Auto,Auto" ColumnDefinitions="*,Auto">
                            <TextBlock Grid.Row="0" Grid.Column="0"
                                       Text="Repositories"
                                       FontSize="16" FontWeight="SemiBold"
                                       Foreground="{DynamicResource TextFillColorPrimaryBrush}" />
                            <TextBlock Grid.Row="1" Grid.Column="0"
                                       Text="{Binding TotalRepositories, StringFormat='{}{0} repositories'}"
                                       FontSize="12"
                                       Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                                       Margin="0,2,0,0" />
                            <Button Grid.Row="0" Grid.RowSpan="2" Grid.Column="1"
                                    Content="+"
                                    Command="{Binding AddRepoCommand}"
                                    ToolTip.Tip="Add Repository"
                                    FontSize="16" FontWeight="Bold"
                                    Padding="8,4"
                                    VerticalAlignment="Center" />
                        </Grid>
                    </Border>

                    <!-- Repository List -->
                    <ListBox Grid.Row="1"
                             ItemsSource="{Binding Repositories}"
                             SelectedItem="{Binding SelectedRepository}"
                             Background="Transparent"
                             Margin="8,0"
                             BorderThickness="0">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <Grid ColumnDefinitions="Auto,*" Margin="4,6">
                                    <!-- Status Dot -->
                                    <Ellipse Grid.Column="0"
                                             Width="8" Height="8"
                                             Fill="{Binding StatusColor}"
                                             VerticalAlignment="Top"
                                             Margin="0,5,10,0" />
                                    <!-- Repo Info -->
                                    <StackPanel Grid.Column="1" Spacing="1">
                                        <TextBlock Text="{Binding Name}"
                                                   FontWeight="SemiBold"
                                                   FontSize="13"
                                                   Foreground="{DynamicResource TextFillColorPrimaryBrush}" />
                                        <TextBlock Text="{Binding Path}"
                                                   FontSize="11"
                                                   Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                                                   TextTrimming="CharacterEllipsis" />
                                        <StackPanel Orientation="Horizontal" Spacing="6">
                                            <TextBlock Text="{Binding Status}"
                                                       FontSize="11"
                                                       Foreground="{DynamicResource TextFillColorSecondaryBrush}" />
                                            <TextBlock Text="{Binding LastActivity}"
                                                       FontSize="11"
                                                       Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                                                       IsVisible="{Binding LastActivity, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" />
                                        </StackPanel>
                                    </StackPanel>
                                </Grid>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>

                    <!-- Sidebar Footer: Start All / Stop All -->
                    <Border Grid.Row="2" Padding="12" BorderThickness="0,1,0,0"
                            BorderBrush="{DynamicResource CardStrokeColorDefaultBrush}">
                        <StackPanel Spacing="6">
                            <Button Content="Start All"
                                    Command="{Binding StartAllCommand}"
                                    IsEnabled="{Binding CanStartAll}"
                                    HorizontalAlignment="Stretch"
                                    HorizontalContentAlignment="Center" />
                            <Button Content="Stop All"
                                    Command="{Binding StopAllCommand}"
                                    IsEnabled="{Binding IsRunning}"
                                    HorizontalAlignment="Stretch"
                                    HorizontalContentAlignment="Center" />
                        </StackPanel>
                    </Border>
                </Grid>
            </Border>

            <!-- Sidebar / Content Separator -->
            <Border Grid.Column="1" Width="1"
                    Background="{DynamicResource CardStrokeColorDefaultBrush}" />

            <!-- ==================== CONTENT AREA ==================== -->
            <Grid Grid.Column="2" RowDefinitions="Auto,*">

                <!-- Selected Repository Header -->
                <Border Grid.Row="0" Padding="24,20"
                        BorderThickness="0,0,0,1"
                        BorderBrush="{DynamicResource CardStrokeColorDefaultBrush}">
                    <!-- When a repo is selected -->
                    <Grid IsVisible="{Binding SelectedRepository, Converter={x:Static ObjectConverters.IsNotNull}}">
                        <StackPanel Spacing="6">
                            <TextBlock Text="{Binding SelectedRepository.Name}"
                                       FontSize="20" FontWeight="SemiBold"
                                       Foreground="{DynamicResource TextFillColorPrimaryBrush}" />
                            <TextBlock Text="{Binding SelectedRepository.Path}"
                                       FontSize="13"
                                       Foreground="{DynamicResource TextFillColorSecondaryBrush}" />
                            <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,4,0,0">
                                <Ellipse Width="8" Height="8"
                                         Fill="{Binding SelectedRepository.StatusColor}"
                                         VerticalAlignment="Center" />
                                <TextBlock Text="{Binding SelectedRepository.Status}"
                                           FontSize="13"
                                           Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                                           VerticalAlignment="Center" />
                                <TextBlock Text="·" FontSize="13"
                                           Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                                           VerticalAlignment="Center" />
                                <TextBlock Text="{Binding SelectedRepository.LastActivity, StringFormat='Last activity: {0}'}"
                                           FontSize="13"
                                           Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                                           VerticalAlignment="Center" />
                            </StackPanel>
                            <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,6,0,0">
                                <Button Content="Start"
                                        Command="{Binding StartRepoCommand}"
                                        CommandParameter="{Binding SelectedRepository}"
                                        IsEnabled="{Binding SelectedRepository.CanStart}" />
                                <Button Content="Stop"
                                        Command="{Binding StopRepoCommand}"
                                        CommandParameter="{Binding SelectedRepository}"
                                        IsEnabled="{Binding SelectedRepository.IsRunning}" />
                                <Button Content="Remove"
                                        Command="{Binding RemoveRepoCommand}"
                                        CommandParameter="{Binding SelectedRepository}" />
                            </StackPanel>
                        </StackPanel>
                    </Grid>
                </Border>

                <!-- Placeholder when no repo selected (overlays the header area) -->
                <Border Grid.Row="0" Padding="24,20"
                        IsVisible="{Binding SelectedRepository, Converter={x:Static ObjectConverters.IsNull}}">
                    <TextBlock Text="Select a repository"
                               FontSize="16"
                               Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                               VerticalAlignment="Center"
                               HorizontalAlignment="Center" />
                </Border>

                <!-- Activity Log -->
                <Grid Grid.Row="1" RowDefinitions="Auto,*" Margin="0">
                    <!-- Log Header -->
                    <Border Grid.Row="0" Padding="24,12"
                            BorderThickness="0,0,0,1"
                            BorderBrush="{DynamicResource CardStrokeColorDefaultBrush}">
                        <Grid ColumnDefinitions="*,Auto">
                            <TextBlock Grid.Column="0" Text="Activity Log"
                                       FontSize="14" FontWeight="SemiBold"
                                       Foreground="{DynamicResource TextFillColorPrimaryBrush}"
                                       VerticalAlignment="Center" />
                            <Button Grid.Column="1" Content="Clear"
                                    Command="{Binding ClearLogCommand}"
                                    Padding="12,4" />
                        </Grid>
                    </Border>

                    <!-- Log Entries -->
                    <ListBox Grid.Row="1"
                             ItemsSource="{Binding LogEntries}"
                             Background="Transparent"
                             BorderThickness="0"
                             Padding="8,4">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal" Spacing="12" Margin="8,2">
                                    <TextBlock Text="{Binding Timestamp, StringFormat='{}{0:HH:mm:ss}'}"
                                               FontFamily="Cascadia Mono, Consolas, Menlo, monospace"
                                               FontSize="12"
                                               Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                                               Width="60" />
                                    <TextBlock Text="{Binding Level}"
                                               FontFamily="Cascadia Mono, Consolas, Menlo, monospace"
                                               FontSize="12"
                                               Foreground="{Binding LevelColor}"
                                               Width="45"
                                               FontWeight="SemiBold" />
                                    <TextBlock Text="{Binding Repository}"
                                               FontFamily="Cascadia Mono, Consolas, Menlo, monospace"
                                               FontSize="12"
                                               Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                                               Width="100"
                                               TextTrimming="CharacterEllipsis" />
                                    <TextBlock Text="{Binding Message}"
                                               FontFamily="Cascadia Mono, Consolas, Menlo, monospace"
                                               FontSize="12"
                                               Foreground="{DynamicResource TextFillColorPrimaryBrush}"
                                               TextWrapping="Wrap" />
                                </StackPanel>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </Grid>
            </Grid>
        </Grid>

        <!-- ==================== STATUS BAR ==================== -->
        <Border Grid.Row="1"
                Background="{DynamicResource CardBackgroundFillColorDefaultBrush}"
                BorderBrush="{DynamicResource CardStrokeColorDefaultBrush}"
                BorderThickness="0,1,0,0"
                Padding="16,8">
            <Grid ColumnDefinitions="*,Auto">
                <TextBlock Grid.Column="0"
                           Text="{Binding StatusMessage}"
                           FontSize="12"
                           Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                           VerticalAlignment="Center" />
                <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="16">
                    <TextBlock Text="{Binding ActiveRepositories, StringFormat='Active: {0}'}"
                               FontSize="12"
                               Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                               VerticalAlignment="Center" />
                    <TextBlock Text="{Binding TotalRepositories, StringFormat='Total: {0}'}"
                               FontSize="12"
                               Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                               VerticalAlignment="Center" />
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</Window>
```

**Step 2: Build to verify no XAML errors**

Run: `dotnet build GitAutoSync.GUI/GitAutoSync.GUI.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add GitAutoSync.GUI/Views/MainWindow.axaml
git commit -m "feat(ui): rewrite MainWindow with sidebar layout and theme resources"
```

---

### Task 4: Run the app and verify visually

**Files:**
- None (visual verification only)

**Step 1: Build the full solution**

Run: `dotnet build GitAutoSync.sln`
Expected: Build succeeded with 0 errors

**Step 2: Commit all changes**

```bash
git add -A
git commit -m "feat(ui): modern sidebar UI with Fluent theme resources

- Replace hardcoded hex colors with DynamicResource theme brushes
- Sidebar layout: repo list left, detail + log right
- System-following dark/light mode (RequestedThemeVariant=Default)
- Custom status color resources (running/error/stopped)
- Monospace font for activity log
- Status dots for repository state
- No new dependencies added"
```
