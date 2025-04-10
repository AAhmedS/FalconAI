import cv2
import numpy as np
import math
import time

# Function to calculate Euclidean distance
def calculate_distance(point1, point2):
    return math.sqrt((point2[0] - point1[0])**2 + (point2[1] - point1[1])**2)

# Function to calculate angle between three points (in degrees)
def calculate_angle(point1, center, point2):
    a = calculate_distance(center, point1)
    b = calculate_distance(center, point2)
    c = calculate_distance(point1, point2)
    try:
        angle = math.degrees(math.acos((a**2 + b**2 - c**2) / (2 * a * b)))
        return angle
    except:
        return 0  # Handle division by zero or invalid triangle

# Simulated landmark detection (replace with actual model like MediaPipe)
def detect_landmarks_and_ball(frame):
    # Convert to grayscale for simple detection
    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    
    # Simulated landmarks (hardcoded for demonstration)
    landmarks = {
        'left_shoulder': [200, 100],
        'right_shoulder': [300, 100],
        'left_elbow': [180, 200],
        'right_elbow': [320, 200],
        'left_wrist': [170, 300],
        'right_wrist': [330, 300],
        'neck': [250, 80],
        'waist': [250, 300],
        'left_hip': [220, 350],
        'right_hip': [280, 350],
        'left_knee': [210, 450],
        'right_knee': [290, 450],
        'left_ankle': [200, 550],
        'right_ankle': [300, 550]
    }
    
    # Simple ball detection using Hough Circle Transform (assumes a red ball)
    hsv = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)
    lower_red = np.array([0, 120, 70])
    upper_red = np.array([10, 255, 255])
    mask = cv2.inRange(hsv, lower_red, upper_red)
    circles = cv2.HoughCircles(gray, cv2.HOUGH_GRADIENT, dp=1, minDist=20,
                               param1=50, param2=30, minRadius=5, maxRadius=15)
    
    ball_center = [250, 600]  # Default if no ball detected
    ball_radius = 5
    if circles is not None:
        circles = np.uint16(np.around(circles))
        for i in circles[0, :]:
            ball_center = [i[0], i[1]]
            ball_radius = i[2]
            break
    
    return landmarks, ball_center, ball_radius

# Main function to process live camera feed
def process_camera():
    # Open the default camera
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("Error: Could not open camera.")
        return
    
    print("Press 'q' to capture and process the frame.")
    
    while True:
        ret, frame = cap.read()
        if not ret:
            print("Error: Could not read frame.")
            break
        
        # Show live feed
        cv2.imshow("Live Feed - Press 'q' to Capture", frame)
        
        # Wait for 'q' key to capture and process
        if cv2.waitKey(1) & 0xFF == ord('q'):
            # Detect landmarks and ball
            landmarks, ball_center, ball_radius = detect_landmarks_and_ball(frame)
            
            # Scale factor based on ball size (assuming ball size is 5 units)
            scale_factor = ball_radius / 5
            
            # Calculate measurements
            shoulder_width = calculate_distance(landmarks['left_shoulder'], landmarks['right_shoulder'])
            left_arm_length = calculate_distance(landmarks['left_shoulder'], landmarks['left_elbow']) + \
                             calculate_distance(landmarks['left_elbow'], landmarks['left_wrist'])
            right_arm_length = calculate_distance(landmarks['right_shoulder'], landmarks['right_elbow']) + \
                              calculate_distance(landmarks['right_elbow'], landmarks['right_wrist'])
            body_length = calculate_distance(landmarks['neck'], landmarks['waist'])
            left_leg_angle = calculate_angle(landmarks['left_hip'], landmarks['left_knee'], landmarks['left_ankle'])
            right_leg_angle = calculate_angle(landmarks['right_hip'], landmarks['right_knee'], landmarks['right_ankle'])
            
            # Scale measurements
            shoulder_width_scaled = shoulder_width / scale_factor
            left_arm_length_scaled = left_arm_length / scale_factor
            right_arm_length_scaled = right_arm_length / scale_factor
            body_length_scaled = body_length / scale_factor
            
            # Draw landmarks
            for name, (x, y) in landmarks.items():
                cv2.circle(frame, (int(x), int(y)), 5, (0, 255, 0), -1)
                cv2.putText(frame, name, (int(x) + 10, int(y)), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1)
            
            # Draw lines for measurements
            cv2.line(frame, landmarks['left_shoulder'], landmarks['right_shoulder'], (255, 0, 0), 2)
            cv2.line(frame, landmarks['left_shoulder'], landmarks['left_elbow'], (0, 255, 255), 2)
            cv2.line(frame, landmarks['left_elbow'], landmarks['left_wrist'], (0, 255, 255), 2)
            cv2.line(frame, landmarks['right_shoulder'], landmarks['right_elbow'], (0, 255, 255), 2)
            cv2.line(frame, landmarks['right_elbow'], landmarks['right_wrist'], (0, 255, 255), 2)
            cv2.line(frame, landmarks['neck'], landmarks['waist'], (255, 255, 0), 2)
            cv2.line(frame, landmarks['left_hip'], landmarks['left_knee'], (255, 0, 255), 2)
            cv2.line(frame, landmarks['left_knee'], landmarks['left_ankle'], (255, 0, 255), 2)
            cv2.line(frame, landmarks['right_hip'], landmarks['right_knee'], (255, 0, 255), 2)
            cv2.line(frame, landmarks['right_knee'], landmarks['right_ankle'], (255, 0, 255), 2)
            
            # Draw the ball
            cv2.circle(frame, (int(ball_center[0]), int(ball_center[1])), int(ball_radius), (0, 0, 255), -1)
            
            # Add text for measurements
            cv2.putText(frame, f"Shoulder Width: {shoulder_width_scaled:.2f}", (10, 20), 
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
            cv2.putText(frame, f"Left Arm: {left_arm_length_scaled:.2f}", (10, 40), 
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
            cv2.putText(frame, f"Right Arm: {right_arm_length_scaled:.2f}", (10, 60), 
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
            cv2.putText(frame, f"Body Length: {body_length_scaled:.2f}", (10, 80), 
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
            cv2.putText(frame, f"Left Leg Angle: {left_leg_angle:.2f}", (10, 100), 
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
            cv2.putText(frame, f"Right Leg Angle: {right_leg_angle:.2f}", (10, 120), 
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
            
            # Save and display the processed frame
            cv2.imwrite("body_measurements_live.png", frame)
            cv2.imshow("Processed Frame", frame)
            cv2.waitKey(0)  # Wait until a key is pressed to close the window
            break
    
    # Release the camera and close windows
    cap.release()
    cv2.destroyAllWindows()
    
    # Print measurements
    print(f"Shoulder Width: {shoulder_width_scaled:.2f} units")
    print(f"Left Arm Length: {left_arm_length_scaled:.2f} units")
    print(f"Right Arm Length: {right_arm_length_scaled:.2f} units")
    print(f"Body Length: {body_length_scaled:.2f} units")
    print(f"Left Leg Curve Angle: {left_leg_angle:.2f} degrees")
    print(f"Right Leg Curve Angle: {right_leg_angle:.2f} degrees")

# Run the program
if __name__ == "__main__":
    process_camera()