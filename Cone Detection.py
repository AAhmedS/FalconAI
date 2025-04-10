import cv2
import mediapipe as mp
from ultralytics import YOLO
import numpy as np

# Initialize MediaPipe Pose
mp_pose = mp.solutions.pose
mp_drawing = mp.solutions.drawing_utils
pose = mp_pose.Pose(min_detection_confidence=0.5, min_tracking_confidence=0.5)

# Initialize YOLOv11 models
# Using nano version for better real-time performance
ball_model = YOLO("yolo11n.pt")  # Ball detection
cone_model = YOLO("yolo11n.pt")  # Cone detection

# Define class names for balls and cones (assuming COCO dataset classes)
BALL_CLASS_ID = 32  # 'sports ball' in COCO dataset
CONE_CLASS_ID = None  # No direct cone class in COCO, we'll use a workaround

def process_frame(frame):
    # Convert the BGR image to RGB for MediaPipe
    image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    
    # Process body landmarks with MediaPipe
    pose_results = pose.process(image_rgb)
    
    # Convert back to BGR for OpenCV display
    image = cv2.cvtColor(image_rgb, cv2.COLOR_RGB2BGR)
    
    # Draw body landmarks
    if pose_results.pose_landmarks:
        mp_drawing.draw_landmarks(
            image,
            pose_results.pose_landmarks,
            mp_pose.POSE_CONNECTIONS,
            mp_drawing.DrawingSpec(color=(245, 117, 66), thickness=2, circle_radius=2),
            mp_drawing.DrawingSpec(color=(245, 66, 230), thickness=2, circle_radius=2)
        )
    
    # Detect balls using YOLOv11
    ball_results = ball_model.predict(image, conf=0.5, classes=[BALL_CLASS_ID])
    for r in ball_results:
        boxes = r.boxes
        for box in boxes:
            x1, y1, x2, y2 = map(int, box.xyxy[0])
            conf = box.conf[0]
            cv2.rectangle(image, (x1, y1), (x2, y2), (0, 255, 0), 2)
            cv2.putText(image, f"Ball {conf:.2f}", (x1, y1-10), 
                       cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2)
    
    # Detect cones using YOLOv11
    # Note: Since COCO doesn't have a direct "cone" class, we'll look for similar shapes
    # Using class 67 ('traffic cone' isn't in COCO, so we'll approximate with similar objects)
    cone_results = cone_model.predict(image, conf=0.5)
    for r in cone_results:
        boxes = r.boxes
        for box in boxes:
            x1, y1, x2, y2 = map(int, box.xyxy[0])
            conf = box.conf[0]
            cls = int(box.cls[0])
            # Filter for potential cone-like objects (customize as needed)
            if cls in [39, 67]:  # bottle or traffic cone approximation
                cv2.rectangle(image, (x1, y1), (x2, y2), (0, 0, 255), 2)
                cv2.putText(image, f"Cone {conf:.2f}", (x1, y1-10), 
                          cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 255), 2)
    
    return image

def main():
    # Open webcam (use 0 for default camera, or provide video file path)
    cap = cv2.VideoCapture(0)
    
    if not cap.isOpened():
        print("Error: Could not open video source.")
        return
    
    while True:
        ret, frame = cap.read()
        if not ret:
            print("Error: Could not read frame.")
            break
        
        # Process the frame
        processed_frame = process_frame(frame)
        
        # Display the result
        cv2.imshow('Body Landmarks, Ball & Cone Detection', processed_frame)
        
        # Break loop on 'q' key press
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break
    
    # Cleanup
    cap.release()
    cv2.destroyAllWindows()
    pose.close()

if __name__ == "__main__":
    main()