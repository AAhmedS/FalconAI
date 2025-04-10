import cv2
import numpy as np
from ultralytics import YOLO
import matplotlib.pyplot as plt
from typing import Tuple, List, Optional

class SprintAnalyzer:
    def __init__(
        self,
        video_path: str,
        pose_model_path: str,
        cone_model_path: str,
        use_normalization: bool = True,
        use_noise_reduction: bool = True,
        use_motion_blur_reduction: bool = False,
        use_background_subtraction: bool = False,
        use_roi_cropping: bool = False,
        target_width: int = 640,
        target_height: int = 480
    ):
        self.video_path = video_path
        self.pose_model = YOLO(pose_model_path)
        self.cone_model = YOLO(cone_model_path)
        self.cap = cv2.VideoCapture(video_path)
        self.fps = self.cap.get(cv2.CAP_PROP_FPS)
        self.frame_count = int(self.cap.get(cv2.CAP_PROP_FRAME_COUNT))
        self.distance = 10.0  # meters
        self.bg_subtractor = cv2.createBackgroundSubtractorMOG2(history=100, varThreshold=50) if use_background_subtraction else None
        self.use_normalization = use_normalization
        self.use_noise_reduction = use_noise_reduction
        self.use_motion_blur_reduction = use_motion_blur_reduction
        self.use_background_subtraction = use_background_subtraction
        self.use_roi_cropping = use_roi_cropping
        self.target_width = target_width
        self.target_height = target_height
        self.roi = None  # Will be set based on cone positions if enabled

    def resize_frame(self, frame: np.ndarray) -> np.ndarray:
        return cv2.resize(frame, (self.target_width, self.target_height), interpolation=cv2.INTER_AREA)

    def normalize_frame(self, frame: np.ndarray) -> np.ndarray:
        return cv2.normalize(frame, None, 0, 255, cv2.NORM_MINMAX)

    def reduce_noise(self, frame: np.ndarray) -> np.ndarray:
        return cv2.GaussianBlur(frame, (5, 5), 0)

    def reduce_motion_blur(self, frame: np.ndarray) -> np.ndarray:
        kernel = np.array([[0, -1, 0], [-1, 5, -1], [0, -1, 0]])
        return cv2.filter2D(frame, -1, kernel)

    def subtract_background(self, frame: np.ndarray) -> np.ndarray:
        fg_mask = self.bg_subtractor.apply(frame)
        return cv2.bitwise_and(frame, frame, mask=fg_mask)

    def crop_roi(self, frame: np.ndarray) -> np.ndarray:
        if self.roi is None:
            return frame
        x, y, w, h = self.roi
        return frame[y:y+h, x:x+w]

    def preprocess_frame(self, frame: np.ndarray) -> np.ndarray:
        frame = self.resize_frame(frame)
        if self.use_normalization:
            frame = self.normalize_frame(frame)
        if self.use_noise_reduction:
            frame = self.reduce_noise(frame)
        if self.use_motion_blur_reduction:
            frame = self.reduce_motion_blur(frame)
        if self.use_background_subtraction and self.bg_subtractor is not None:
            frame = self.subtract_background(frame)
        if self.use_roi_cropping and self.roi is not None:
            frame = self.crop_roi(frame)
        return frame

    def detect_cones(self, frame: np.ndarray) -> Tuple[Optional[float], Optional[float]]:
        results = self.cone_model(frame)
        left_x, right_x = None, None
        for r in results:
            boxes = r.boxes.xyxy.cpu().numpy()
            for box in boxes:
                x_center = (box[0] + box[2]) / 2
                if left_x is None or x_center < left_x:
                    left_x = x_center
                if right_x is None or x_center > right_x:
                    right_x = x_center
        return left_x, right_x

    def detect_pose(self, frame: np.ndarray) -> Optional[float]:
        results = self.pose_model(frame)
        if results and len(results) > 0:
            keypoints = results[0].keypoints.xy.cpu().numpy()
            if len(keypoints) > 0:
                hip_left = keypoints[0][11][0]  # Left hip
                hip_right = keypoints[0][12][0]  # Right hip
                return (hip_left + hip_right) / 2
        return None

    def set_roi_from_cones(self, left_x: float, right_x: float, frame_width: int, frame_height: int):
        margin = 50  # Pixels around cones
        x_min = max(0, int(left_x - margin))
        x_max = min(frame_width, int(right_x + margin))
        self.roi = (x_min, 0, x_max - x_min, frame_height)

    def process_video(self) -> Tuple[float, float, List[Tuple[float, float]]]:
        start_frame, end_frame = None, None
        distance_time_data = []
        frame_num = 0

        left_cone_x, right_cone_x = None, None
        while self.cap.isOpened():
            ret, frame = self.cap.read()
            if not ret:
                break

            original_frame = frame.copy()  # For ROI setting
            frame = self.preprocess_frame(frame)
            current_time = frame_num / self.fps

            if frame_num == 0:
                left_cone_x, right_cone_x = self.detect_cones(frame)
                if left_cone_x is None or right_cone_x is None:
                    raise ValueError("Could not detect both cones in the first frame")
                if self.use_roi_cropping:
                    self.set_roi_from_cones(left_cone_x, right_cone_x, 
                                          original_frame.shape[1], original_frame.shape[0])

            player_x = self.detect_pose(frame)
            if player_x is not None:
                if start_frame is None and player_x > left_cone_x:
                    start_frame = frame_num
                    distance_time_data.append((0, current_time))
                elif start_frame is not None and player_x > right_cone_x:
                    end_frame = frame_num
                    distance_time_data.append((self.distance, current_time))
                    break
                elif start_frame is not None:
                    relative_pos = (player_x - left_cone_x) / (right_cone_x - left_cone_x)
                    distance = relative_pos * self.distance
                    distance_time_data.append((distance, current_time))

            frame_num += 1

        self.cap.release()
        if start_frame is None or end_frame is None:
            raise ValueError("Could not determine start or end of sprint")

        return start_frame / self.fps, end_frame / self.fps, distance_time_data

    def calculate_speed(self, start_time: float, end_time: float) -> float:
        duration = end_time - start_time
        return self.distance / duration if duration > 0 else 0

    def generate_graph(self, distance_time_data: List[Tuple[float, float]], output_path: str):
        distances, times = zip(*distance_time_data)
        plt.figure(figsize=(10, 6))
        plt.plot(times, distances, '-o', color='blue', linewidth=2)
        plt.xlabel('Time (seconds)')
        plt.ylabel('Distance (meters)')
        plt.title('Distance-Time Graph: 10m Sprint')
        plt.grid(True)
        plt.savefig(output_path)
        plt.close()

    def overlay_results(self, output_video_path: str):
        self.cap = cv2.VideoCapture(self.video_path)
        fourcc = cv2.VideoWriter_fourcc(*'mp4v')
        out = cv2.VideoWriter(output_video_path, fourcc, self.fps, 
                            (self.target_width, self.target_height))

        while self.cap.isOpened():
            ret, frame = self.cap.read()
            if not ret:
                break

            frame = self.preprocess_frame(frame)

            cone_results = self.cone_model(frame)
            for r in cone_results:
                boxes = r.boxes.xyxy.cpu().numpy()
                for box in boxes:
                    x1, y1, x2, y2 = map(int, box)
                    cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 2)

            pose_results = self.pose_model(frame)
            for r in pose_results:
                keypoints = r.keypoints.xy.cpu().numpy()
                if len(keypoints) > 0:
                    for kp in keypoints[0]:
                        x, y = map(int, kp)
                        cv2.circle(frame, (x, y), 5, (0, 0, 255), -1)

            out.write(frame)

        self.cap.release()
        out.release()

def main():
    video_path = "sprintVideo.mp4"
    pose_model_path = "yolov8nPose.pt"
    cone_model_path = "customCone_model.pt"
    
    analyzer = SprintAnalyzer(
        video_path=video_path,
        pose_model_path=pose_model_path,
        cone_model_path=cone_model_path,
        use_normalization=True,
        use_noise_reduction=True,
        use_motion_blur_reduction=False,
        use_background_subtraction=False,
        use_roi_cropping=True
    )
    
    try:
        start_time, end_time, distance_time_data = analyzer.process_video()
        speed = analyzer.calculate_speed(start_time, end_time)
        
        print(f"Start Time: {start_time:.2f} s")
        print(f"End Time: {end_time:.2f} s")
        print(f"Duration: {end_time - start_time:.2f} s")
        print(f"Average Speed: {speed:.2f} m/s")
        
        analyzer.generate_graph(distance_time_data, "distance_time_graph.png")
        analyzer.overlay_results("video.mp4")
        
    except Exception as e:
        print(f"Error: {str(e)}")

if __name__ == "__main__":
    main()