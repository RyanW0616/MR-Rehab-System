# MR Rehabilitation System - Core Algorithms and Data Analysis

This repository contains the core algorithms and statistical analysis scripts used in our study. The scripts demonstrate the core logic of the MR system (gesture detection and precision alignment task) and generate the figures presented in the manuscript, based on the anonymized clinical and training data of three participants (A001, A002, and A003).

## Repository Structure

MR-Rehab-System/
├── README.md
├── data/
│   ├── participant_clinical_outcomes.csv   # BBT and NHPT clinical outcomes
│   ├── A001_data_train2.csv                # Training 2 data for Participant A001
│   ├── A002_data_train2.csv                # Training 2 data for Participant A002
│   ├── A003_data_train2.csv                # Training 2 data for Participant A003
│   ├── A001_data_train4.csv                # Training 4 data for Participant A001
│   ├── A002_data_train4.csv                # Training 4 data for Participant A002
│   └── A003_data_train4.csv                # Training 4 data for Participant A003
├── core_algorithms/
│   ├── AI_Manager.cs                       # Full implementation of gesture detection and AI voice assistant
│   └── BlockController.cs                  # Full implementation of precision alignment task and stacking accuracy calculation
└── analysis_scripts/
    ├── paired_hedges_g_forest_plot_mdpi.py         # Generates Figure 5 (Effect sizes forest plot)
    ├── individual_clinical_trends_plot_mdpi.py     # Generates Figure 6 (Individual clinical trends)
    ├── train2_posture_restriction_plot_mdpi.py     # Generates Figure 7 (Posture restriction impact)
    ├── train2_shots_and_hit_rate_plot_mdpi.py      # Generates Figure 8 (Total shots and hit rate)
    └── train4_stacking_and_area_plot_mdpi.py       # Generates Figures 9 & 10 (Stacking accuracy and movement economy)

## Core Algorithms (Unity C# Scripts)

The `core_algorithms/` folder contains two complete Unity C# scripts that implement the key mathematical models described in the manuscript.

- `AI_Manager.cs`: Implements the spatial geometric computation using wrist and finger joint nodes to detect the palm-facing-up gesture (dot > 0.9) and trigger the AI rehabilitation assistant (Section 4.2).
- `BlockController.cs`: Implements the raycasting and discrete sampling method to quantify the surface overlap ratio between virtual blocks for the precision alignment task (Section 4.3).

Note: These scripts are provided as-is from the Unity project. They may contain references to other Unity components (e.g., XRHand, GameObjects) that are not included in this repository. The scripts are intended for illustrating the core logic rather than being directly runnable without the full project environment.

**Important:** API keys are intentionally left blank. To run these scripts, you must obtain your own API keys from Groq and OpenAI, and fill them in via the Unity Inspector.

## Requirements

- Python 3.8+
- pandas
- numpy
- matplotlib
- seaborn

You can install the required packages using:
pip install pandas numpy matplotlib seaborn

## Usage

1. Clone this repository to your local machine.
2. Ensure that all `.csv` files are placed in the `data/` folder and the Python scripts are in the `analysis_scripts/` folder.
3. Run the scripts from the `analysis_scripts/` directory. For example:
   python paired_hedges_g_forest_plot_mdpi.py
4. The scripts will automatically read the corresponding data from the `data/` folder and output high-resolution `.png` figures suitable for MDPI journal submission.

## Data Privacy

All data have been anonymized. Participants are identified only as A001, A002, and A003. No personally identifiable information (PII) is included in this repository.

## Contact

For any questions or inquiries regarding the code or data, please contact:
- Name: Shan You Wang
- Affiliation: Department of Information Engineering and Computer Science, Feng Chia University, Taichung, Taiwan (Alumnus)
- Email: tom930616@gmail.com