https://www.zhihu.com/question/424627189/answer/3069074313


# Hikvision GigE Vision Network Port Industrial Camera Python Control Library hik_camera

I have been engaged in industrial vision research and development since 2019. In the past four years, I have tested and used more than 10 industrial camera models and deployed more than 50 industrial cameras in different projects.

I mainly use Hikvision's network port industrial cameras. Since Hikvision's official Python API is too complex and cumbersome, I developed and encapsulated a more Pythonic Hikvision industrial camera control library: [hik_camera](https://github.com/DIYer22/hik_camera/)

This library has the following features:
- Modular design, extremely concise Pythonic API, easy to carry out business development and easy for others to quickly get started
- Various knowledge and experience of industrial cameras are precipitated into code
- Rich in functions, supports Windows and Linux systems, and provides pre-compiled Docker images

There are two ways to install hik_camera:
- Docker solution
- `docker run --net=host -v /tmp:/tmp -it diyer22/hik_camera`
- Manual installation solution
1. Install the official driver: Download and install the "Machine Vision Industrial Camera Client MVS SDK" for the corresponding operating system on the [Hikvision Robotics Official Website](https://www.hikrobotics.com/cn/machinevision/service/download)
2. `pip install hik_camera`

```bash
# Connect to the camera, test whether the installation is successful
python -m hik_camera.hik_camera
```

Sample code for taking pictures:  
```Python
from hik_camera import HikCamera
ips = HikCamera.get_all_ips()
print("All camera IP adresses:", ips)
ip = ips[0]
cam = HikCamera(ip)
with cam:
   img = cam.robust_get_frame()
   print("Saveing image to:", cam.save(img)) 
   # The image will be automatically saved to a temporary folder
```
For more detailed examples and camera parameter configuration methods (such as exposure, gain, pixel format, etc.), please visit the hik_camera GitHub homepage:

https://github.com/DIYer22/hik_camera/