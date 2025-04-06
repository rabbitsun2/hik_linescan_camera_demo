using MvCameraControl;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace BasicDemoLineScan
{
    public partial class Form1 : Form
    {
        readonly DeviceTLayerType enumTLayerType = DeviceTLayerType.MvGigEDevice | DeviceTLayerType.MvUsbDevice
                | DeviceTLayerType.MvGenTLGigEDevice | DeviceTLayerType.MvGenTLCXPDevice | DeviceTLayerType.MvGenTLCameraLinkDevice | DeviceTLayerType.MvGenTLXoFDevice;

        IDevice device = null;
        List<IDeviceInfo> deviceInfos = new List<IDeviceInfo>();

        bool isGrabbing = false;
        Thread receiveThread = null;

        // kr:드라이버로부터 얻은 프레임 정보에 사용 | en:Frame info that getting image from driver
        IFrameOut frameForSave = null;
        private readonly Object lockForSaveImage = new Object();

        IEnumValue triggerSelector = null;  // 트리거 옵션
        IEnumValue triggerMode = null;      // 트리거 모드
        IEnumValue triggerSource = null;    // 트리거 소스
        IEnumValue pixelFormat = null;      // 픽셀 형식
        IEnumValue imgCompressMode = null;  // HB 모드
        IEnumValue preampGain = null;       // 아날로그 게인

        public Form1()
        {
            InitializeComponent();

            SDKSystem.Initialize();

            UpdateDeviceList();
            CheckForIllegalCrossThreadCalls = false;
        }

        /// <summary>
        /// // kr:오류 메시지 표시 | en:Show error message
        /// </summary>
        /// <param name="message">kr:오류 메시지 | en: error message</param>
        /// <param name="errorCode">kr:오류 코드 | en: error code</param>
        private void ShowErrorMsg(string message, int errorCode)
        {
            string errorMsg;
            if (errorCode == 0)
            {
                errorMsg = message;
            }
            else
            {
                errorMsg = message + ": Error =" + String.Format("{0:X}", errorCode);
            }

            switch (errorCode)
            {
                case MvError.MV_E_HANDLE: errorMsg += " Error or invalid handle "; break;
                case MvError.MV_E_SUPPORT: errorMsg += " Not supported function "; break;
                case MvError.MV_E_BUFOVER: errorMsg += " Cache is full "; break;
                case MvError.MV_E_CALLORDER: errorMsg += " Function calling order error "; break;
                case MvError.MV_E_PARAMETER: errorMsg += " Incorrect parameter "; break;
                case MvError.MV_E_RESOURCE: errorMsg += " Applying resource failed "; break;
                case MvError.MV_E_NODATA: errorMsg += " No data "; break;
                case MvError.MV_E_PRECONDITION: errorMsg += " Precondition error, or running environment changed "; break;
                case MvError.MV_E_VERSION: errorMsg += " Version mismatches "; break;
                case MvError.MV_E_NOENOUGH_BUF: errorMsg += " Insufficient memory "; break;
                case MvError.MV_E_UNKNOW: errorMsg += " Unknown error "; break;
                case MvError.MV_E_GC_GENERIC: errorMsg += " General error "; break;
                case MvError.MV_E_GC_ACCESS: errorMsg += " Node accessing condition error "; break;
                case MvError.MV_E_ACCESS_DENIED: errorMsg += " No permission "; break;
                case MvError.MV_E_BUSY: errorMsg += " Device is busy, or network disconnected "; break;
                case MvError.MV_E_NETER: errorMsg += " Network error "; break;
            }

            MessageBox.Show(errorMsg, "PROMPT");
        }

        /// <summary>
        /// kr:장치 열거 | en:Enum devices
        /// </summary>
        private void bnEnum_Click(object sender, EventArgs e)
        {
            UpdateDeviceList();
        }

        /// <summary>
        /// kr:장치 목록 새로 고침 | en:Update devices list
        /// </summary>
        private void UpdateDeviceList()
        {
            // kr:장치 목록 열거 | en:Enumerate Device List
            cmbDeviceList.Items.Clear();

            int result = DeviceEnumerator.EnumDevices(enumTLayerType, out deviceInfos);
            if (result != MvError.MV_OK)
            {
                ShowErrorMsg("Enumerate devices fail!", result);
                return;
            }

            // kr:양식 목록에 장치 이름 표시 | en:Display device name in the form list
            for (int i = 0; i < deviceInfos.Count; i++)
            {
                IDeviceInfo deviceInfo = deviceInfos[i];
                if (deviceInfo.UserDefinedName != "")
                {
                    cmbDeviceList.Items.Add(deviceInfo.TLayerType.ToString() + ": " + deviceInfo.UserDefinedName + " (" + deviceInfo.SerialNumber + ")");
                }
                else
                {
                    cmbDeviceList.Items.Add(deviceInfo.TLayerType.ToString() + ": " + deviceInfo.ManufacturerName + " " + deviceInfo.ModelName + " (" + deviceInfo.SerialNumber + ")");
                }
            }

            // kr:첫 번째 항목을 선택하세요. | en:Select the first item
            if (deviceInfos.Count > 0)
            {
                cmbDeviceList.SelectedIndex = 0;
            }
            else
            {
                ShowErrorMsg("No device", 0);
            }
            return;
        }

        /// <summary>
        /// kr:장치를 켜십시오 | en:Open device
        /// </summary>
        private void bnOpen_Click(object sender, System.EventArgs e)
        {
            if (0 == deviceInfos.Count || -1 == cmbDeviceList.SelectedIndex)
            {
                ShowErrorMsg("No device, please enumerate device", 0);
                return;
            }

            // kr:선택한 장치 정보 가져오기 | en:Get selected device information
            IDeviceInfo deviceInfo = deviceInfos[cmbDeviceList.SelectedIndex];

            try
            {
                // kr:장치를 켜십시오 | en:Open device
                device = DeviceFactory.CreateDevice(deviceInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Create Device fail!" + ex.Message);
                return;
            }

            int result = device.Open();
            if (result != MvError.MV_OK)
            {
                device.Dispose();
                device = null;

                ShowErrorMsg("Open Device fail!", result);
                return;
            }

            //kr: Gige 장치인지 확인 | en: Determine whether it is a GigE device
            if (device is IGigEDevice)
            {
                //kr: Gige 장치로 변환 | en: Convert to Gige device
                IGigEDevice gigEDevice = device as IGigEDevice;

                // kr:네트워크의 최적 패킷 크기 감지(GigE 카메라에만 유효) | en:Detection network optimal package size(It only works for the GigE camera)
                int optionPacketSize;
                result = gigEDevice.GetOptimalPacketSize(out optionPacketSize);
                if (result != MvError.MV_OK)
                {
                    ShowErrorMsg("Warning: Get Packet Size failed!", result);
                }
                else
                {
                    result = device.Parameters.SetIntValue("GevSCPSPacketSize", (long)optionPacketSize);
                    if (result != MvError.MV_OK)
                    {
                        ShowErrorMsg("Warning: Set Packet Size failed!", result);
                    }
                }
            }

            // kr:획득 연속 모드 설정 | en:Set Continues Aquisition Mode
            device.Parameters.SetEnumValueByString("AcquisitionMode", "Continuous");
            device.Parameters.SetEnumValueByString("TriggerMode", "Off");

            // kr:매개변수 가져오기 | en:Get parameters
            GetImageCompressionMode();
            GetPreampGain();
            GetTriggerMode();
            GetTriggerSelector();
            GetTriggerSource();
            GetPixelFormat();
            bnGetParam_Click(null, null);

            // kr:제어 작업 | en:Control operation
            btnOpen.Enabled = false;
            btnClose.Enabled = true;
            btnStartGrab.Enabled = true;
            btnStopGrab.Enabled = false;
            btnTriggerExec.Enabled = false;
            btnGetParam.Enabled = true;
            btnSetParam.Enabled = true;
            cmbDeviceList.Enabled = false;
        }

        /// <summary>
        /// kr:가상 이득 모드 얻기 | en:Get PreampGain
        /// </summary>
        private void GetPreampGain()
        {
            cmbPreampGain.Items.Clear();

            int result = device.Parameters.GetEnumValue("PreampGain", out preampGain);
            if (result == MvError.MV_OK)
            {
                for (int i = 0; i < preampGain.SupportedNum; i++)
                {
                    cmbPreampGain.Items.Add(preampGain.SupportEnumEntries[i].Symbolic);
                    if (preampGain.SupportEnumEntries[i].Symbolic == preampGain.CurEnumEntry.Symbolic)
                    {
                        cmbPreampGain.SelectedIndex = i;
                    }
                }
                cmbPreampGain.Enabled = true;
            }
        }

        /// <summary>
        /// kr:HB 모드 가져오기 | en:Get ImageCompressionMode
        /// </summary>
        private void GetImageCompressionMode()
        {
            cmbHBMode.Items.Clear();

            int result = device.Parameters.GetEnumValue("ImageCompressionMode", out imgCompressMode);
            if (result == MvError.MV_OK)
            {
                for (int i = 0; i < imgCompressMode.SupportedNum; i++)
                {
                    cmbHBMode.Items.Add(imgCompressMode.SupportEnumEntries[i].Symbolic);
                    if (imgCompressMode.SupportEnumEntries[i].Symbolic == imgCompressMode.CurEnumEntry.Symbolic)
                    {
                        cmbHBMode.SelectedIndex = i;
                    }
                }
                cmbHBMode.Enabled = true;
            }
            else
            {
                cmbHBMode.Enabled = false;
            }
        }

        /// <summary>
        /// kr:픽셀 형식 가져오기 | en:Get PixelFormat
        /// </summary>
        private void GetPixelFormat()
        {
            cmbPixelFormat.Items.Clear();

            int result = device.Parameters.GetEnumValue("PixelFormat", out pixelFormat);
            if (result == MvError.MV_OK)
            {
                for (int i = 0; i < pixelFormat.SupportedNum; i++)
                {
                    cmbPixelFormat.Items.Add(pixelFormat.SupportEnumEntries[i].Symbolic);
                    if (pixelFormat.SupportEnumEntries[i].Symbolic == pixelFormat.CurEnumEntry.Symbolic)
                    {
                        cmbPixelFormat.SelectedIndex = i;
                    }
                }
                cmbPixelFormat.Enabled = true;
            }
        }

        /// <summary>
        /// kr:트리거 옵션 가져오기 | en:Get TriggerSelector
        /// </summary>
        private void GetTriggerSelector()
        {
            cmbTriggerSelector.Items.Clear();
            int result = device.Parameters.GetEnumValue("TriggerSelector", out triggerSelector);
            if (result == MvError.MV_OK)
            {
                for (int i = 0; i < triggerSelector.SupportedNum; i++)
                {
                    cmbTriggerSelector.Items.Add(triggerSelector.SupportEnumEntries[i].Symbolic);
                    if (triggerSelector.SupportEnumEntries[i].Symbolic == triggerSelector.CurEnumEntry.Symbolic)
                    {
                        cmbTriggerSelector.SelectedIndex = i;
                    }
                }
                cmbTriggerSelector.Enabled = true;
            }
        }

        /// <summary>
        /// kr:트리거 모드 가져오기 | en:Get TriggerMode
        /// </summary>
        private void GetTriggerMode()
        {
            cmbTriggerMode.Items.Clear();
            int result = device.Parameters.GetEnumValue("TriggerMode", out triggerMode);
            if (result == MvError.MV_OK)
            {
                for (int i = 0; i < triggerMode.SupportedNum; i++)
                {
                    cmbTriggerMode.Items.Add(triggerMode.SupportEnumEntries[i].Symbolic);
                    if (triggerMode.SupportEnumEntries[i].Symbolic == triggerMode.CurEnumEntry.Symbolic)
                    {
                        cmbTriggerMode.SelectedIndex = i;
                    }
                }
                cmbTriggerMode.Enabled = true;
            }
        }

        /// <summary>
        /// kr:트리거 소스 가져오기 | en:Get TriggerSource
        /// </summary>
        private void GetTriggerSource()
        {
            cmbTriggerSource.Items.Clear();
            int result = device.Parameters.GetEnumValue("TriggerSource", out triggerSource);
            if (result == MvError.MV_OK)
            {
                for (int i = 0; i < triggerSource.SupportedNum; i++)
                {
                    cmbTriggerSource.Items.Add(triggerSource.SupportEnumEntries[i].Symbolic);
                    if (triggerSource.SupportEnumEntries[i].Value == triggerSource.CurEnumEntry.Value)
                    {
                        cmbTriggerSource.SelectedIndex = i;
                    }
                }
                cmbTriggerSource.Enabled = true;
            }
        }

        /// <summary>
        /// kr:장치를 끄십시오 | en:Close device
        /// </summary>
        private void bnClose_Click(object sender, System.EventArgs e)
        {
            // kr:스트림 플래그 지우기 | en:Reset flow flag bit
            if (isGrabbing == true)
            {
                isGrabbing = false;
                receiveThread.Join();
            }

            // kr:장치를 끄십시오 | en:Close Device
            if (device != null)
            {
                device.Close();
                device.Dispose();
            }

            // kr:제어 작업 | en:Control Operation
            SetCtrlWhenClose();
        }

        private void SetCtrlWhenClose()
        {
            btnOpen.Enabled = true;
            btnClose.Enabled = false;
            btnStartGrab.Enabled = false;
            btnStopGrab.Enabled = false;
            btnTriggerExec.Enabled = false;
            cmbDeviceList.Enabled = true;

            btnSaveBmp.Enabled = false;
            btnSaveJpg.Enabled = false;
            btnSaveTiff.Enabled = false;
            btnSavePng.Enabled = false;
            tbExposure.Enabled = false;
            btnGetParam.Enabled = false;
            btnSetParam.Enabled = false;
            cmbPixelFormat.Enabled = false;
            cmbHBMode.Enabled = false;
            cmbPreampGain.Enabled = false;
            cmbTriggerSource.Enabled = false;
            cmbTriggerSelector.Enabled = false;
            cmbTriggerMode.Enabled = false;
            tbExposure.Enabled = false;
            tbDigitalShift.Enabled = false;
            tbAcqLineRate.Enabled = false;
            chkLineRateEnable.Enabled = false;
        }

        /// <summary>
        /// kr:이미지 스레드 수신 | en:Receive image thread process
        /// </summary>
        public void ReceiveThreadProcess()
        {
            IFrameOut frameOut = null;
            int result = MvError.MV_OK;

            while (isGrabbing)
            {
                result = device.StreamGrabber.GetImageBuffer(1000, out frameOut);
                if (result == MvError.MV_OK)
                {
                    // kr:이미지 데이터 저장은 이미지 파일을 저장하는 데 사용 | en:Save frame info for save image
                    lock (lockForSaveImage)
                    {
                        try
                        {
                            frameForSave = frameOut.Clone() as IFrameOut;
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show("IFrameOut.Clone failed, " + e.Message);
                            return;
                        }
                    }

                    // kr:이미지 데이터 렌더링 | en:Display frame
                    device.ImageRender.DisplayOneFrame(pictureBox1.Handle, frameOut.Image);

                    // kr:릴리스 프레임 정보 | en:Free frame info
                    device.StreamGrabber.FreeImageBuffer(frameOut);
                }
                else
                {
                    if (cmbTriggerMode.SelectedText == "On")
                    {
                        Thread.Sleep(5);
                    }
                }
            }
        }

        /// <summary>
        /// kr:수집 시작 | en:Start grab
        /// </summary>
        private void bnStartGrab_Click(object sender, System.EventArgs e)
        {
            // kr:플래그 비트 세트 true | en:Set position bit true
            isGrabbing = true;

            receiveThread = new Thread(ReceiveThreadProcess);
            receiveThread.Start();

            // kr:수집 시작 | en:Start Grabbing
            int result = device.StreamGrabber.StartGrabbing();
            if (result != MvError.MV_OK)
            {
                isGrabbing = false;
                receiveThread.Join();
                ShowErrorMsg("Start Grabbing Fail!", result);
                return;
            }

            // kr:제어 작업 | en:Control Operation
            SetCtrlWhenStartGrab();
        }

        private void SetCtrlWhenStartGrab()
        {
            btnStartGrab.Enabled = false;
            btnStopGrab.Enabled = true;

            if ((cmbTriggerMode.Text == "On") && (cmbTriggerSource.Text == "Software") && isGrabbing)
            {
                btnTriggerExec.Enabled = true;
            }

            btnSaveBmp.Enabled = true;
            btnSaveJpg.Enabled = true;
            btnSaveTiff.Enabled = true;
            btnSavePng.Enabled = true;
            cmbPixelFormat.Enabled = false;
            cmbHBMode.Enabled = false;
        }

        /// <summary>
        /// kr:소프트 트리거는 한 번 실행 | en:Trigger once by software
        /// </summary>
        private void bnTriggerExec_Click(object sender, System.EventArgs e)
        {
            // ch:触发命令 | en:Trigger command
            int result = device.Parameters.SetCommandValue("TriggerSoftware");
            if (result != MvError.MV_OK)
            {
                ShowErrorMsg("Trigger Software Fail!", result);
            }
        }

        /// <summary>
        /// kr:수집 중지 | en:Stop Grab
        /// </summary>
        private void bnStopGrab_Click(object sender, System.EventArgs e)
        {
            // kr:플래그 비트가 false로 설정됨 | en:Set flag bit false
            isGrabbing = false;
            receiveThread.Join();

            // kr:수집 중지 | en:Stop Grabbing
            int result = device.StreamGrabber.StopGrabbing();
            if (result != MvError.MV_OK)
            {
                ShowErrorMsg("Stop Grabbing Fail!", result);
            }

            // kr:제어 작업 | en:Control Operation
            SetCtrlWhenStopGrab();
        }

        private void SetCtrlWhenStopGrab()
        {
            btnStartGrab.Enabled = true;
            btnStopGrab.Enabled = false;
            btnTriggerExec.Enabled = false;
            btnSaveBmp.Enabled = false;
            btnSaveJpg.Enabled = false;
            btnSaveTiff.Enabled = false;
            btnSavePng.Enabled = false;
            cmbPixelFormat.Enabled = true;
            cmbHBMode.Enabled = true;
        }

        /// <summary>
        /// kr:이미지 저장 | en:Save image
        /// </summary>
        /// <param name="imageFormatInfo">kr:이미지 형식 정보 | en:Image format info </param>
        /// <returns></returns>
        private int SaveImage(ImageFormatInfo imageFormatInfo)
        {
            if (frameForSave == null)
            {
                throw new Exception("No vaild image");
            }

            string imagePath = "Image_w" + frameForSave.Image.Width.ToString() + "_h" + frameForSave.Image.Height.ToString() + "_fn" + frameForSave.FrameNum.ToString() + "." + imageFormatInfo.FormatType.ToString();

            lock (lockForSaveImage)
            {
                return device.ImageSaver.SaveImageToFile(imagePath, frameForSave.Image, imageFormatInfo, CFAMethod.Equilibrated);
            }
        }

        /// <summary>
        /// kr:BMP 파일 저장 | en:Save Bmp image
        /// </summary>
        private void bnSaveBmp_Click(object sender, System.EventArgs e)
        {
            int result;

            try
            {
                ImageFormatInfo imageFormatInfo = new ImageFormatInfo();
                imageFormatInfo.FormatType = ImageFormatType.Bmp;

                result = SaveImage(imageFormatInfo);
                if (result != MvError.MV_OK)
                {
                    ShowErrorMsg("Save Image Fail!", result);
                    return;
                }
                else
                {
                    ShowErrorMsg("Save Image Succeed!", 0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save Image Failed, " + ex.Message);
                return;
            }
        }

        /// <summary>
        /// kr:JPEG 파일 저장 | en:Save Jpeg image
        /// </summary>
        private void bnSaveJpg_Click(object sender, System.EventArgs e)
        {
            int result;

            try
            {
                ImageFormatInfo imageFormatInfo = new ImageFormatInfo();
                imageFormatInfo.FormatType = ImageFormatType.Jpeg;
                imageFormatInfo.JpegQuality = 80;

                result = SaveImage(imageFormatInfo);
                if (result != MvError.MV_OK)
                {
                    ShowErrorMsg("Save Image Fail!", result);
                    return;
                }
                else
                {
                    ShowErrorMsg("Save Image Succeed!", 0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save Image Failed, " + ex.Message);
                return;
            }
        }

        /// <summary>
        /// kr:Tiff 형식 파일 저장 | en:Save Tiff image
        /// </summary>
        private void bnSaveTiff_Click(object sender, System.EventArgs e)
        {
            int result;
            try
            {
                ImageFormatInfo imageFormatInfo = new ImageFormatInfo();
                imageFormatInfo.FormatType = ImageFormatType.Tiff;

                result = SaveImage(imageFormatInfo);
                if (result != MvError.MV_OK)
                {
                    ShowErrorMsg("Save Image Fail!", result);
                    return;
                }
                else
                {
                    ShowErrorMsg("Save Image Succeed!", 0);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Save Image Failed, " + ex.Message);
                return;
            }
        }

        /// <summary>
        /// kr:PNG 형식 파일 저장 | en:Save PNG image
        /// </summary>
        private void bnSavePng_Click(object sender, System.EventArgs e)
        {
            int result;

            try
            {
                ImageFormatInfo imageFormatInfo = new ImageFormatInfo();
                imageFormatInfo.FormatType = ImageFormatType.Png;

                result = SaveImage(imageFormatInfo);
                if (result != MvError.MV_OK)
                {
                    ShowErrorMsg("Save Image Fail!", result);
                    return;
                }
                else
                {
                    ShowErrorMsg("Save Image Succeed!", 0);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Save Image Failed, " + ex.Message);
                return;
            }
        }

        /// <summary>
        /// kr:매개변수 설정 | en:Set Parameters
        /// </summary>
        private void bnSetParam_Click(object sender, System.EventArgs e)
        {
            int result = MvError.MV_OK;

            // kr:노출 설정 | en:Set ExposureTime
            if (tbExposure.Enabled)
            {
                try
                {
                    float.Parse(tbExposure.Text);
                    device.Parameters.SetEnumValue("ExposureAuto", 0);
                    result = device.Parameters.SetFloatValue("ExposureTime", float.Parse(tbExposure.Text));
                    if (result != MvError.MV_OK)
                    {
                        ShowErrorMsg("Set Exposure Time Fail!", result);
                    }
                }
                catch
                {
                    ShowErrorMsg("Please enter ExposureTime correct", 0);
                }
            }

            // kr:디지털 게인 설정 | en:Set DigitalShift
            if (tbDigitalShift.Enabled)
            {
                try
                {
                    float.Parse(tbDigitalShift.Text);
                    device.Parameters.SetBoolValue("DigitalShiftEnable", true);
                    result = device.Parameters.SetFloatValue("DigitalShift", float.Parse(tbDigitalShift.Text));
                    if (result != MvError.MV_OK)
                    {
                        ShowErrorMsg("Set Digital Shift Fail!", result);
                    }
                }
                catch
                {
                    ShowErrorMsg("Please enter DigitalShift correct", 0);
                }
            }

            // kr:라인 주파수 설정값을 설정 | en:Set AcquisitionLineRate
            if (tbAcqLineRate.Enabled)
            {
                try
                {
                    int.Parse(tbAcqLineRate.Text);
                    result = device.Parameters.SetIntValue("AcquisitionLineRate", int.Parse(tbAcqLineRate.Text));
                    if (result != MvError.MV_OK)
                    {
                        ShowErrorMsg("Set Acquisition Line Rate Fail!", result);
                    }
                }
                catch
                {
                    ShowErrorMsg("Please enter AcquisitionLineRate correct", 0);
                }
            }
        }

        /// <summary>
        /// kr:매개변수 가져오기 | en:Get Parameters
        /// </summary>
        private void bnGetParam_Click(object sender, System.EventArgs e)
        {
            // kr:노출 매개변수 가져오기 | en:Get ExposureTime
            IFloatValue exposureTime = null;
            int result = device.Parameters.GetFloatValue("ExposureTime", out exposureTime);
            if (result == MvError.MV_OK)
            {
                tbExposure.Text = exposureTime.CurValue.ToString("F2");
                tbExposure.Enabled = true;
            }

            // kr:디지털 이득 매개변수 가져오기 | en:Get DigitalShift
            IFloatValue digitalShift = null;
            result = device.Parameters.GetFloatValue("DigitalShift", out digitalShift);
            if (result == MvError.MV_OK)
            {
                tbDigitalShift.Text = digitalShift.CurValue.ToString("F2");
                tbDigitalShift.Enabled = true;
            }

            // kr:수평 주파수 활성화 스위치 가져오기 | en:Get AcquisitionLineRateEnable
            bool acqLineRateEnable = false;
            result = device.Parameters.GetBoolValue("AcquisitionLineRateEnable", out acqLineRateEnable);
            if (result == MvError.MV_OK)
            {
                chkLineRateEnable.Enabled = true;
                chkLineRateEnable.Checked = acqLineRateEnable;
            }

            // kr:라인 주파수 설정 값 가져오기 | en:Get AcquisitionLineRate
            IIntValue acqLineRate = null;
            result = device.Parameters.GetIntValue("AcquisitionLineRate", out acqLineRate);
            if (result == MvError.MV_OK)
            {
                tbAcqLineRate.Text = acqLineRate.CurValue.ToString();
                tbAcqLineRate.Enabled = true;
            }

            // kr:라인 주파수의 실제 값을 얻으세요 | en:Get ResultingLineRate
            IIntValue resultLineRate = null;
            result = device.Parameters.GetIntValue("ResultingLineRate", out resultLineRate);
            if (result == MvError.MV_OK)
            {
                tbResLineRate.Text = resultLineRate.CurValue.ToString();
                tbResLineRate.Enabled = true;
            }
        }

        private void cbTriggerSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            int result = device.Parameters.SetEnumValue("TriggerSelector", triggerSelector.SupportEnumEntries[cmbTriggerSelector.SelectedIndex].Value);
            if (result != MvError.MV_OK)
            {
                ShowErrorMsg("Set Trigger Selector Failed", result);
                for (int i = 0; i < triggerSelector.SupportedNum; i++)
                {
                    if (triggerSelector.SupportEnumEntries[i].Value == triggerSelector.CurEnumEntry.Value)
                    {
                        cmbTriggerSelector.SelectedIndex = i;
                        return;
                    }
                }
            }

            GetTriggerMode();
            GetTriggerSource();
        }

        private void cbTiggerMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            int result = device.Parameters.SetEnumValue("TriggerMode", (uint)triggerMode.SupportEnumEntries[cmbTriggerMode.SelectedIndex].Value);
            if (result != MvError.MV_OK)
            {
                ShowErrorMsg("Set Trigger Mode Failed", result);
                for (int i = 0; i < triggerMode.SupportedNum; i++)
                {
                    if (triggerMode.SupportEnumEntries[i].Value == triggerMode.CurEnumEntry.Value)
                    {
                        cmbTriggerMode.SelectedIndex = i;
                        return;
                    }
                }
            }

            GetTriggerSource();

            if ((cmbTriggerMode.Text == "On" && cmbTriggerSource.Text == "Software") && isGrabbing)
            {
                btnTriggerExec.Enabled = true;
            }
            else
            {
                btnTriggerExec.Enabled = false;
            }
        }

        private void cbTriggerSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            int result = device.Parameters.SetEnumValue("TriggerSource", triggerSource.SupportEnumEntries[cmbTriggerSource.SelectedIndex].Value);
            if (result != MvError.MV_OK)
            {
                ShowErrorMsg("Set Trigger Source Failed", result);
                for (int i = 0; i < triggerSource.SupportedNum; i++)
                {
                    if (triggerSource.SupportEnumEntries[i].Value == triggerSource.CurEnumEntry.Value)
                    {
                        cmbTriggerSource.SelectedIndex = i;
                        return;
                    }
                }
            }

            if ((cmbTriggerMode.Text == "On" && cmbTriggerSource.Text == "Software") && isGrabbing)
            {
                btnTriggerExec.Enabled = true;
            }
            else
            {
                btnTriggerExec.Enabled = false;
            }
        }

        private void cbPixelFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            // kr:픽셀 형식 설정 | en:Set PixelFormat
            int result = device.Parameters.SetEnumValue("PixelFormat", pixelFormat.SupportEnumEntries[cmbPixelFormat.SelectedIndex].Value);
            if (result != MvError.MV_OK)
            {
                ShowErrorMsg("Set PixelFormat Fail!", result);
                for (int i = 0; i < pixelFormat.SupportedNum; i++)
                {
                    if (pixelFormat.SupportEnumEntries[i].Value == pixelFormat.CurEnumEntry.Value)
                    {
                        cmbPixelFormat.SelectedIndex = i;
                        return;
                    }
                }
            }
            GetImageCompressionMode();
        }

        private void cbHBMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            // kr:무손실 압축 모드 설정 | en:Set ImageCompressionMode
            int result = device.Parameters.SetEnumValue("ImageCompressionMode", imgCompressMode.SupportEnumEntries[cmbHBMode.SelectedIndex].Value);
            if (result != MvError.MV_OK)
            {
                ShowErrorMsg("Set ImageCompressionMode Fail!", result);
                for (int i = 0; i < imgCompressMode.SupportedNum; i++)
                {
                    if (imgCompressMode.SupportEnumEntries[i].Value == imgCompressMode.CurEnumEntry.Value)
                    {
                        cmbHBMode.SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        private void cbPreampGain_SelectedIndexChanged(object sender, EventArgs e)
        {
            int result = device.Parameters.SetEnumValue("PreampGain", preampGain.SupportEnumEntries[cmbPreampGain.SelectedIndex].Value);
            if (result != MvError.MV_OK)
            {
                ShowErrorMsg("Set PreampGain Fail!", result);
                for (int i = 0; i < preampGain.SupportedNum; i++)
                {
                    if (preampGain.SupportEnumEntries[i].Value == preampGain.CurEnumEntry.Value)
                    {
                        cmbPreampGain.SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        private void chkLineRateEnable_CheckedChanged(object sender, EventArgs e)
        {
            if (chkLineRateEnable.Checked)
            {
                device.Parameters.SetBoolValue("AcquisitionLineRateEnable", true);
            }
            else
            {
                device.Parameters.SetBoolValue("AcquisitionLineRateEnable", false);
            }
        }

        /// <summary>
        /// kr:창 닫기 이벤트 | en: FormClosing event
        /// </summary>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            bnClose_Click(sender, e);

            SDKSystem.Finalize();
        }
    }
}
