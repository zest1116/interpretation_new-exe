using LGCNS.axink.Common;
using LGCNS.axink.Models.ApiResponse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LGCNS.axink.App.Windows
{
    /// <summary>
    /// CompanySelectWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CompanySelectWindow : Window
    {
        public string SelectedCompanyCode { get; private set; }

        public CompanySelectWindow(string? tenantListUrl, string? companyCode)
        {
            InitializeComponent();

            GetTenantList(tenantListUrl, companyCode);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private async void GetTenantList(string? tenantListUrl, string? companyCode)
        {
            if (!string.IsNullOrEmpty(tenantListUrl))
            {
                var tenants = await ApiClient.GetAsync<List<TenantInfo>>(tenantListUrl);

                CmbCompanies.ItemsSource = tenants;

                if (!string.IsNullOrEmpty(companyCode))
                {
                    var selectedCompany = tenants?.Find(x => x.CompanyCd == companyCode);
                    if (selectedCompany != null)
                    {
                        CmbCompanies.SelectedItem = selectedCompany;
                    }
                }
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (CmbCompanies.SelectedItem is not TenantInfo selected)
            {
                TxtError.Text = Application.Current.Resources["Msg_SelectCompany_PlaceHolder"].ToString();
                return;
            }

            SelectedCompanyCode = selected.CompanyCd!;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void CmbCompanies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TxtError.Text = string.Empty;
        }

    }
}
