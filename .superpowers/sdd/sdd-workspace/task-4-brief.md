# Task 4: Create `PortViewerControl` (XAML + Code-behind)

**Files:**
- Create: `UI/Views/PortViewerControl.xaml`
- Create: `UI/Views/PortViewerControl.xaml.cs`

**Interfaces:**
- Consumes: `PortViewModel` from Task 3 (set as DataContext)

## What To Do

Create a WPF UserControl for the port viewer. The XAML has a search/filter toolbar at the top and a DataGrid showing connections below. The code-behind handles double-click and button events.

The dark theme uses these brushes (defined in MainWindow.xaml):
- BgBrush: #0f172a
- SurfaceBrush: #1e293b
- SurfaceAltBrush: #273549
- BorderBrush: #334155
- TextPrimary: #f1f5f9
- TextSecondary: #94a3b8
- AccentBrush: #3b82f6

The ToolbarButton style is already defined in MainWindow.xaml for use by toolbar buttons.

## XAML File

Create `UI/Views/PortViewerControl.xaml`:

```xml
<UserControl x:Class="OctoTask.UI.Views.PortViewerControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="clr-namespace:OctoTask.UI.Converters"
             xmlns:vm="clr-namespace:OctoTask.UI.ViewModels"
             FontFamily="Cascadia Mono, Consolas, Courier New">

    <UserControl.Resources>
        <conv:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
    </UserControl.Resources>

    <DockPanel>
        <!-- Toolbar: search + filter + refresh -->
        <Border DockPanel.Dock="Top"
                Background="{StaticResource SurfaceBrush}"
                BorderBrush="{StaticResource BorderBrush}"
                BorderThickness="0,0,0,1"
                Padding="8,6">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="120"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="100"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Port search -->
                <TextBlock Grid.Column="0" Text="Port:" Foreground="{StaticResource TextSecondary}"
                           VerticalAlignment="Center" Margin="0,0,8,0" FontSize="12"/>
                <TextBox Grid.Column="1" Width="100" Height="24"
                         VerticalAlignment="Center"
                         Text="{Binding PortFilter, UpdateSourceTrigger=PropertyChanged}"
                         Background="{StaticResource BgBrush}"
                         Foreground="{StaticResource TextPrimary}"
                         BorderBrush="{StaticResource BorderBrush}"
                         BorderThickness="1"
                         FontFamily="Cascadia Mono, Consolas, Courier New"
                         FontSize="12" Padding="6,2">
                    <TextBox.Resources>
                        <Style TargetType="Border">
                            <Setter Property="CornerRadius" Value="4"/>
                        </Style>
                    </TextBox.Resources>
                </TextBox>

                <!-- Protocol filter -->
                <TextBlock Grid.Column="2" Text="Protocol:" Foreground="{StaticResource TextSecondary}"
                           VerticalAlignment="Center" Margin="12,0,8,0" FontSize="12"/>
                <ComboBox Grid.Column="3" Width="80" Height="24"
                          VerticalAlignment="Center"
                          SelectedItem="{Binding ProtocolFilter}"
                          Background="{StaticResource BgBrush}"
                          Foreground="{StaticResource TextPrimary}"
                          BorderBrush="{StaticResource BorderBrush}"
                          BorderThickness="1" FontSize="12">
                    <ComboBoxItem Content="All" IsSelected="True"/>
                    <ComboBoxItem Content="TCP"/>
                    <ComboBoxItem Content="UDP"/>
                </ComboBox>

                <!-- Separator -->
                <Separator Grid.Column="4" Margin="8,0"/>

                <!-- Refresh -->
                <Button Grid.Column="5"
                        Command="{Binding RefreshCommand}"
                        Content="&#x27F3; Refresh"
                        Style="{StaticResource ToolbarButton}"/>

                <CheckBox Grid.Column="6"
                          IsChecked="{Binding IsAutoRefreshEnabled}"
                          Content="Auto (5s)"
                          Foreground="{StaticResource TextSecondary}"
                          Margin="8,0,0,0"
                          VerticalAlignment="Center"/>

                <!-- Spacer -->
                <Border Grid.Column="7"/>

                <!-- Go to Process button -->
                <Button Grid.Column="8"
                        Content="&#x2192; Go to Process"
                        Style="{StaticResource ToolbarButton}"
                        IsEnabled="{Binding CanGoToProcess}"
                        Click="OnGoToProcessClick"/>
            </Grid>
        </Border>

        <!-- DataGrid fills remaining space -->
        <DataGrid ItemsSource="{Binding Connections}"
                  AutoGenerateColumns="False"
                  IsReadOnly="True"
                  SelectionMode="Single"
                  SelectedItem="{Binding SelectedConnection}"
                  Background="{StaticResource BgBrush}"
                  Foreground="{StaticResource TextPrimary}"
                  BorderBrush="{StaticResource BorderBrush}"
                  BorderThickness="0"
                  GridLinesVisibility="None"
                  HeadersVisibility="Column"
                  RowBackground="{StaticResource BgBrush}"
                  AlternatingRowBackground="{StaticResource SurfaceBrush}"
                  CanUserAddRows="False"
                  CanUserSortColumns="True"
                  CanUserResizeColumns="True"
                  RowHeight="28"
                  FontFamily="Cascadia Mono, Consolas, Courier New"
                  FontSize="12"
                  MouseDoubleClick="OnDataGridDoubleClick">

            <DataGrid.Columns>
                <DataGridTextColumn Header="Protocol"
                                    Binding="{Binding ProtocolDisplay}"
                                    SortMemberPath="ProtocolDisplay"
                                    Width="70"/>
                <DataGridTextColumn Header="Local Address"
                                    Binding="{Binding LocalAddress}"
                                    SortMemberPath="LocalAddress"
                                    Width="140"/>
                <DataGridTextColumn Header="Local Port"
                                    Binding="{Binding LocalPortDisplay}"
                                    SortMemberPath="LocalPort"
                                    Width="90">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType="TextBlock">
                            <Setter Property="HorizontalAlignment" Value="Right"/>
                            <Setter Property="Padding" Value="4,0"/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>
                <DataGridTextColumn Header="Remote Address"
                                    Binding="{Binding RemoteAddress}"
                                    SortMemberPath="RemoteAddress"
                                    Width="140"/>
                <DataGridTextColumn Header="Remote Port"
                                    Binding="{Binding RemotePortDisplay}"
                                    SortMemberPath="RemotePort"
                                    Width="90">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType="TextBlock">
                            <Setter Property="HorizontalAlignment" Value="Right"/>
                            <Setter Property="Padding" Value="4,0"/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>
                <DataGridTextColumn Header="State"
                                    Binding="{Binding StateDisplay}"
                                    SortMemberPath="State"
                                    Width="130"/>
                <DataGridTextColumn Header="PID"
                                    Binding="{Binding PidDisplay}"
                                    SortMemberPath="Pid"
                                    Width="70">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType="TextBlock">
                            <Setter Property="HorizontalAlignment" Value="Right"/>
                            <Setter Property="Padding" Value="4,0"/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>
                <DataGridTextColumn Header="Process"
                                    Binding="{Binding ProcessName}"
                                    SortMemberPath="ProcessName"
                                    Width="*"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

## Code-behind File

Create `UI/Views/PortViewerControl.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OctoTask.UI.ViewModels;

namespace OctoTask.UI.Views
{
    public partial class PortViewerControl : UserControl
    {
        public PortViewerControl()
        {
            InitializeComponent();
        }

        private void OnDataGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PortViewModel vm && vm.CanGoToProcess)
            {
                vm.GoToProcess();
            }
        }

        private void OnGoToProcessClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is PortViewModel vm && vm.CanGoToProcess)
            {
                vm.GoToProcess();
            }
        }
    }
}
```

## Verification

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED
