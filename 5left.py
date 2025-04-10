import cv2
import numpy as np
import pandas as pd
import mediapipe as mp
import pandas as pd
import matplotlib.pyplot as plt


# Initialize Mediapipe Pose estimation
mp_pose = mp.solutions.pose
pose = mp_pose.Pose()

# Load the video
video_path = 'kid2.mp4'
cap = cv2.VideoCapture(video_path)

# Check if the video was opened successfully
if not cap.isOpened():
    print("Error: Could not open video.")
    exit()

fps = cap.get(cv2.CAP_PROP_FPS)  # Get the frames per second of the video
width = cap.get(cv2.CAP_PROP_FRAME_WIDTH)
height = cap.get(cv2.CAP_PROP_FRAME_HEIGHT)

# Initialize variables for optical flow
ret, prev_frame = cap.read()
if not ret:
    print("Error: Could not read the first frame.")
    exit()

prev_gray = cv2.cvtColor(prev_frame, cv2.COLOR_BGR2GRAY)

# Initialize data collection variables
data = {
    'Frame': [],
    'Time (s)': [],
    'Speed (m/s)': [],
    'Acceleration (m/s^2)': [],
    'Deceleration (m/s^2)': [],
    'Left_Knee_Angle (degrees)': [],
    'Right_Knee_Angle (degrees)': [],
    'Direction Changes': []
}

# Initialize variables for speed and direction change calculation
previous_speed = 0
previous_angle = 0
direction_changes = 0

frame_index = 0  # Initialize frame index

while True:
    ret, frame = cap.read()
    if not ret:
        break

    # Convert the current frame to grayscale
    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

    # Calculate optical flow using Farneback method
    flow = cv2.calcOpticalFlowFarneback(prev_gray, gray, None,
                                        0.5, 3, 15, 3, 5, 1.2, 0)
    
    # Compute the magnitude and angle of flow (this represents the speed)
    magnitude, angle = cv2.cartToPolar(flow[..., 0], flow[..., 1])

    # Average the magnitude and angle over the entire frame to get an estimate of movement
    avg_magnitude = np.mean(magnitude)
    avg_angle = np.mean(angle)

    # Convert magnitude to speed (assuming frame rate is in frames per second)
    speed = avg_magnitude * fps  # This gives a rough speed in arbitrary units

    # Calculate acceleration (difference between current and previous speed)
    acceleration = (speed - previous_speed) * fps

    # Calculate deceleration (if speed is decreasing)
    deceleration = -acceleration if acceleration < 0 else 0

    # Pose estimation for knee angles and other metrics
    results = pose.process(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
    left_knee_angle = right_knee_angle = None

    if results.pose_landmarks:
        landmarks = results.pose_landmarks.landmark
        left_hip = landmarks[mp_pose.PoseLandmark.LEFT_HIP.value]
        right_hip = landmarks[mp_pose.PoseLandmark.RIGHT_HIP.value]
        left_knee = landmarks[mp_pose.PoseLandmark.LEFT_KNEE.value]
        right_knee = landmarks[mp_pose.PoseLandmark.RIGHT_KNEE.value]
        left_ankle = landmarks[mp_pose.PoseLandmark.LEFT_ANKLE.value]
        right_ankle = landmarks[mp_pose.PoseLandmark.RIGHT_ANKLE.value]

        # Function to calculate joint angles
        def calculate_angle(a, b, c):
            angle = np.arctan2(c.y - b.y, c.x - b.x) - np.arctan2(a.y - b.y, a.x - b.x)
            angle = np.abs(angle)
            if angle > np.pi:
                angle = 2 * np.pi - angle
            return np.degrees(angle)

        left_knee_angle = calculate_angle(left_hip, left_knee, left_ankle)
        right_knee_angle = calculate_angle(right_hip, right_knee, right_ankle)

    # Detect direction change based on significant change in average angle
    if abs(avg_angle - previous_angle) > np.pi / 4:  # Example threshold for direction change
        direction_changes += 1

    # Store the data
    frame_number = int(cap.get(cv2.CAP_PROP_POS_FRAMES))
    data['Frame'].append(frame_number)
    data['Time (s)'].append(frame_number / fps)
    data['Speed (m/s)'].append(speed)
    data['Acceleration (m/s^2)'].append(acceleration if acceleration > 0 else 0)
    data['Deceleration (m/s^2)'].append(deceleration)
    data['Left_Knee_Angle (degrees)'].append(left_knee_angle)
    data['Right_Knee_Angle (degrees)'].append(right_knee_angle)
    data['Direction Changes'].append(direction_changes)

    # Update the previous frame, speed, and angle
    prev_gray = gray
    previous_speed = speed
    previous_angle = avg_angle

    frame_index += 1  # Increment frame index

cap.release()

# Convert the collected data to a DataFrame
df = pd.DataFrame(data)

# Save the DataFrame to a CSV file
df.to_csv('agilility.csv', index=False)
print("CSV file generated successfully!")
