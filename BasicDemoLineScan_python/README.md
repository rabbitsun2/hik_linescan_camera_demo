# Hikvision GigE Vision Network Port Industrial Camera Python Control Library
Use Pythonic style to encapsulate Hikvision Network Port Industrial Camera Python SDK, flexible and easy to use, easy to integrate.

## Revision:
- Revision: Do-Yoon Jung(rabbitsun2 at gmail dot com)
- Revision Date: 2024-10-21(Mon)
- I revised example_opencv1.py and example_opencv2.py so that they can be implemented using OpenCV-Python.
ips[0] is recognized in MS Windows, but may not be recognized in Ubuntu Linux. It works if you manually input the IP address as a string and process it.

## ▮ Features
- **Easy to use** Pythonic API:
- Adopt object-oriented encapsulation to facilitate multi-camera management
- Support `with` syntax to call: `with HikCamera() as cam:`
- Simple and intuitive control syntax: `cam["ExposureTime"]=100000`, `print(cam["ExposureTime"])`
- **Robust**: If an error occurs, the camera will automatically reset and retry
- The interface is: `cams.robust_get_frame()`
- Supports obtaining/processing/accessing **raw images** and saving them in **`.dng` format**
- Example see [./test/test_raw.py](./test/test_raw.py)
- Supports automatically taking a photo at a certain interval to adjust the automatic exposure, so as to prevent exposure failure due to not triggering the photo for too long
- See [./test/test_continuous_adjust_exposure.py](./test/test_continuous_adjust_exposure.py) for Example
- Supports **Windows/Linux** systems, with compiled **Docker images** (`diyer22/hik_camera`)
- Supports CS/CU/CE/CA/CH series of GigE Vision network port industrial cameras
- Conveniently packaged as ROS nodes


## ▮ Installation Install
- Docker one-click run:
- `docker run --net=host -v /tmp:/tmp -it diyer22/hik_camera`
- Manual installation solution:
1. Install the official driver: Download and install the corresponding system on the [Hik Robotics official website](https://www.hikrobotics.com/cn/machinevision/service/download) "Machine Vision Industrial Camera Client MVS SDK"
- You need to register to download from the official website, and you can also find the download link for the Linux version in [Dockerfile](Dockerfile)
2. `pip install hik_camera`
3. If you encounter any problems, you can refer to [Dockerfile](Dockerfile) and install manually step by step
- Verification: Connect the camera and verify whether hik_camera is installed successfully:
```bash
$ python -m hik_camera.hik_camera

All camera IP adresses: ['10.101.68.102', '10.101.68.103']
Saveing ​​image to: /tmp/10.101.68.102.jpg
"cam.get_frame" spend time: 0.072249
----------------------------------------
imgs = cams.robust_get_frame()
└── /: dict 2
├── 10.101.68.102: (3036, 4024, 3)uint8
└── 10.101.68.103: (3036, 4024, 3)uint8
"cams.get_frame" spent time: 0.700901
```

## ▮ Usage
### Image collection Demo
```bash
python -m hik_camera.collect_img
```
- After running, an opencv window will pop up to display the camera image stream. Press the `"space"` key to take a photo, and the `Q` key to exit
- The source code of the image collection demo is in [hik_camera/collect_img.py](hik_camera/collect_img.py)

### Python interface
```Python

from hik_camera import HikCamera

ips = HikCamera.get_all_ips()
print("All camera IP adresses:", ips)
ip = ips[0]
cam = HikCamera(ip)

with cam: # OpenDevice using the context of with
   cam["ExposureAuto"] = "Off" # Configuration parameters are consistent with the official Hikvision API
   cam["ExposureTime"] = 50000 # Unit ns
   rgb = cam.robust_get_frame() # rgb's shape is np.uint8(h, w, 3)
print("Saveing ​​image to:", cam.save(rgb, ip + ".jpg"))
```
- For a more comprehensive example, see the "\_\_main\_\_" code at the bottom of [hik_camera/hik_camera.py](hik_camera/hik_camera.py)
- For more camera parameter configuration examples (exposure/Gain/PixelFormat, etc.), see the comment of `HikCamera.setting()` in [hik_camera/hik_camera.py](hik_camera/hik_camera.py#L91)
- Hikvision official configuration item list: [MvCameraNode-CH.csv](hik_camera/MvCameraNode-CH.csv)
- It is recommended to inherit the `HikCamera` class and override the setting function to configure the camera parameters, example: [hik_camera/collect_img.py](hik_camera/collect_img.py)

Advertisement: It is recommended to use the [calibrating](https://github.com/DIYer22/calibrating) library for camera calibration, which can easily calibrate the internal and external parameters of the camera and quickly build a binocular depth camera