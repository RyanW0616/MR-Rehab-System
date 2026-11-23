# MR Rehabilitation System - Core Algorithms and Data Analysis

This repository contains selected core algorithms and data analysis scripts used in the study. The Unity C# scripts demonstrate the MR system's AI voice interaction and precision alignment scoring, while the Python scripts generate figures from anonymized clinical and training data for three participants (A001, A002, and A003).

## Repository Structure

```text
MR-Rehab-System/
├── README.md
├── data/
│   ├── participant_clinical_outcomes.csv   # BBT and NHPT clinical assessment data
│   ├── A001_data_train2.csv                # Dynamic Aiming data for Participant A001
│   ├── A002_data_train2.csv                # Dynamic Aiming data for Participant A002
│   ├── A003_data_train2.csv                # Dynamic Aiming data for Participant A003
│   ├── A001_data_train4.csv                # Precision Alignment data for Participant A001
│   ├── A002_data_train4.csv                # Precision Alignment data for Participant A002
│   └── A003_data_train4.csv                # Precision Alignment data for Participant A003
├── core_algorithms/
│   ├── AI_Manager.cs                       # Palm-up gesture detection and AI voice assistant
│   └── BlockController.cs                  # Virtual-block interaction and stacking accuracy scoring
└── analysis_scripts/
    ├── paired_hedges_g_forest_plot_mdpi.py      # Figure 4: Clinical effect sizes and standardized changes
    ├── train2_posture_restriction_plot_mdpi.py  # Figure 5: Dynamic Aiming hit rates before/after posture restriction
    ├── train2_shots_and_hit_rate_plot_mdpi.py   # Figure 6: Dynamic Aiming shot counts and hit rates
    └── train4_stacking_plot_mdpi.py             # Figure 7: Precision Alignment stacking accuracy and CV
```

## Core Algorithms (Unity C# Scripts)

The `core_algorithms/` folder contains two Unity C# scripts illustrating key system functions:

- `AI_Manager.cs`: Implements palm-up gesture detection and AI voice assistant interactions (Section 4.2).
- `BlockController.cs`: Implements virtual-block interactions and raycasting-based stacking accuracy scoring for the Precision Alignment Task (Section 4.3).

These scripts are extracted from a Unity project and depend on other Unity/XR components not included in this repository. They are provided to illustrate the implementation logic rather than as standalone applications.

**Important:** API keys are not provided. Running the AI voice assistant requires separately configured access to the relevant AI and speech services.

## Requirements

- Python 3.8+
- pandas
- numpy
- matplotlib
- seaborn

Install the Python packages with:

```bash
pip install pandas numpy matplotlib seaborn
```

## Usage

1. Clone or download this repository.
2. Keep the CSV files in `data/` and the Python scripts in `analysis_scripts/`.
3. Run a script from the repository root. For example, to generate Figure 5:

   ```bash
   python analysis_scripts/train2_posture_restriction_plot_mdpi.py --data-dir data --output-dir output
   ```

4. Run the other analysis scripts to generate their corresponding figures. The figures are saved as PNG files in the configured output location.

## Data Privacy

The shared datasets use anonymized participant identifiers (A001, A002, and A003). Personally identifiable information and private API credentials should not be uploaded to this repository.

## Contact

For questions about the code or data, please contact:

- Name: Shan You Wang
- Affiliation: Department of Information Engineering and Computer Science, Feng Chia University, Taichung, Taiwan (Alumnus)
- Email: tom930616@gmail.com
